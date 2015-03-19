using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerScript : MonoBehaviour {

	bool nocollide = false;
	bool invincible = false;
	public float hitpoints;

	Vector3 prevDirection;
	Camera mouseLookCam;
	Camera mainCam;

	float mouseLookY_Rotation;
	float mouseLookX_Rotation;
	float mouseLookSensitivity;
	int mlb = 0; // mouse look button index

	float defaultForwardSpeed = 40.0f;
	public float forwardSpeed;
	public float sidewaysSpeed;

	public float boostCharge;
	float boostDecrement = 21f; // was 17
	//float boostDecrement = 0.0f;
	float boostIncrement;

	float obstacleDamage = 10;
	float enemyDamage = 5;

	public float mouseX_AxisSensitivity;
	public float mouseY_AxisSensitivity;
	public float rollRate;

	Vector3 vecToMainCam;

	GameObject gameManagerRef;

	// Use this for initialization
	void Start () {

		gameManagerRef = GameObject.FindGameObjectWithTag ("GameManager");

		hitpoints = 100.0f;
		boostCharge = 100.0f;

		boostIncrement = boostDecrement * 0.7f;

		mouseLookY_Rotation = 0;
		mouseLookX_Rotation = 0;
		mouseLookSensitivity = 200;

		mouseLookCam = GameObject.Find("MouseLookCam").camera;
		mainCam = GameObject.Find ("Main Camera").camera;
		mouseLookCam.backgroundColor = mainCam.backgroundColor;

		vecToMainCam = mainCam.transform.position - transform.position;

		sidewaysSpeed = defaultForwardSpeed * 0.7f;
		mouseY_AxisSensitivity = 100.0f;
		mouseX_AxisSensitivity = mouseY_AxisSensitivity * 0.35f;
		rollRate = 75.0f;
	}
	
	// Update is called once per frame
	void Update () {

		dampenRigidbodyForces ();
		//Debug.Log ("mouse look cam is at " + mouseLookCam.transform.position);

		Vector3 nearestEnemyPos = getNearestEnemyPos();

		if (Input.GetMouseButtonDown (mlb) || (Input.GetKeyDown(KeyCode.F) && nearestEnemyPos != transform.position)) {
			switchToMouseLookCam();
		}
		else if (Input.GetMouseButtonUp (mlb) || Input.GetKeyUp(KeyCode.F)
		         || (!Input.GetMouseButton(mlb) && nearestEnemyPos == transform.position)) {
			switchToMainCam();
			// reset mouselook camera position
			resetMouseLookCamPosition();
			mouseLookY_Rotation = 0;
			mouseLookX_Rotation = 0;
		}
		// get direction based on mouse movement direction
		float deltaMouseX, deltaMouseY;
		deltaMouseX = Input.GetAxis ("Mouse X");
		deltaMouseY = Input.GetAxis ("Mouse Y");
		// mouse look stuff
		if (Input.GetMouseButton(mlb)) {
			//mouseLookCam.transform.position = mainCam.transform.position;
			//mouseLookCam.transform.rotation = mainCam.transform.rotation;
			mouseLookCam.transform.position = transform.position + (transform.rotation * vecToMainCam);
			mouseLookCam.transform.rotation = transform.rotation;
			mouseLookY_Rotation += Time.deltaTime * deltaMouseX * mouseLookSensitivity;
			mouseLookX_Rotation += Time.deltaTime * deltaMouseY * mouseLookSensitivity;

			//mouseLookX_Rotation = Mathf.Clamp(mouseLookX_Rotation, -5, 5);
			//mouseLookY_Rotation = Mathf.Clamp(mouseLookY_Rotation, -5, 5);

			mouseLookCam.transform.RotateAround(transform.position, transform.right, mouseLookX_Rotation);
			mouseLookCam.transform.RotateAround(transform.position, transform.up, mouseLookY_Rotation);
		}
		else if (Input.GetKey (KeyCode.F) && nearestEnemyPos != transform.position) {
			Debug.DrawLine(transform.position, nearestEnemyPos, Color.cyan);
			Vector3 vecFromEnemy = transform.position - nearestEnemyPos;
			mouseLookCam.transform.position = transform.position + vecFromEnemy.normalized * vecToMainCam.magnitude; //Vector3.Distance(transform.position, mainCam.transform.position);
			mouseLookCam.transform.LookAt(nearestEnemyPos);
		}
		// movement stuff
		if((deltaMouseX != 0 || deltaMouseY != 0) && !Input.GetMouseButton(mlb)){
			//Debug.Log("mouse moved");
			transform.RotateAround(transform.position, transform.up, Time.deltaTime * deltaMouseX * mouseX_AxisSensitivity);
			transform.RotateAround(transform.position, transform.right, Time.deltaTime * -1 * deltaMouseY * mouseY_AxisSensitivity);
		}

		forwardSpeed = defaultForwardSpeed;

		if (GameManagerScript.showWelcomeMsg == true)
			forwardSpeed = 0;

		//Debug.Log ("forward speed is " + forwardSpeed);

		// accelerate (use boost)
		if (Input.GetKey (KeyCode.LeftShift) && boostCharge > 0) {
			forwardSpeed = defaultForwardSpeed * 2.25f;
			boostCharge -= boostDecrement * Time.deltaTime;
		}
		// decelerate
		if (Input.GetKey (KeyCode.Space))
			forwardSpeed = 0; /*forwardSpeed = defaultForwardSpeed * 0.5f;*/

		// forward movement
		Vector3 newPos = transform.position + (transform.TransformDirection (Vector3.forward) * forwardSpeed * Time.deltaTime);
		rigidbody.MovePosition (newPos);

		// sideways strafing
		if (Input.GetKey(KeyCode.A))
			rigidbody.MovePosition (newPos + (transform.TransformDirection(Vector3.left) * sidewaysSpeed * Time.deltaTime));
		if (Input.GetKey(KeyCode.D))
			rigidbody.MovePosition (newPos + (transform.TransformDirection(Vector3.right) * sidewaysSpeed * Time.deltaTime));
		if (Input.GetKey(KeyCode.W))
			rigidbody.MovePosition (newPos + (transform.TransformDirection(Vector3.up) * sidewaysSpeed * Time.deltaTime));
		if (Input.GetKey(KeyCode.S))
			rigidbody.MovePosition (newPos + (transform.TransformDirection(Vector3.down) * sidewaysSpeed * Time.deltaTime));

		// rolling
		Quaternion leftRotation = Quaternion.AngleAxis(rollRate * Time.deltaTime, Vector3.forward);
		Quaternion rightRotation = Quaternion.AngleAxis(-1 * rollRate * Time.deltaTime, Vector3.forward);
		if (Input.GetKey (KeyCode.Q))
			rigidbody.MoveRotation (rigidbody.rotation * leftRotation);
		if (Input.GetKey (KeyCode.E))
			rigidbody.MoveRotation (rigidbody.rotation * rightRotation);

		// recharge boost
		if (!Input.GetKey (KeyCode.LeftShift) && boostCharge < 100.0f)
			boostCharge += boostIncrement * Time.deltaTime;
		if (boostCharge > 100.0f)
			boostCharge = 100.0f;

		//Debug.Log ("boost is at " + boostCharge + ", hitpoints at " + hitpoints);

		checkDead ();

	}

	void resetMouseLookCamPosition() {
		mouseLookCam.transform.rotation = mainCam.transform.rotation;
		mouseLookCam.transform.position = mainCam.transform.position;
	}

	void dampenRigidbodyForces() {
		float cutoff = 0.000001f;
		if (rigidbody.velocity.magnitude > 0 || rigidbody.angularVelocity.magnitude > 0) {
			//Debug.Log ("collision velocity: " + rigidbody.velocity.magnitude + ", collision angular: " + rigidbody.angularVelocity.magnitude);
			rigidbody.velocity *= 0.99999995f * Time.deltaTime;
			rigidbody.angularVelocity *= 0.99999995f * Time.deltaTime;
			if (rigidbody.velocity.magnitude <= cutoff)
				rigidbody.velocity = Vector3.zero;
			if (rigidbody.angularVelocity.magnitude <= cutoff)
				rigidbody.angularVelocity = Vector3.zero;
		}
	}

	void switchToMouseLookCam() {
		mainCam.GetComponent<CameraScript>().enabled = false;
		mainCam.enabled = false;
		mainCam.GetComponent<AudioListener>().enabled = false;
		mouseLookCam.enabled = true;
		mouseLookCam.GetComponent<AudioListener>().enabled = true;
	}

	void switchToMainCam() {
		mainCam.GetComponent<CameraScript>().enabled = true;
		mainCam.enabled = true;
		mainCam.GetComponent<AudioListener>().enabled = true;
		mouseLookCam.enabled = false;
		mouseLookCam.GetComponent<AudioListener>().enabled = false;
	}

	Vector3 getNearestEnemyPos() {
		Collider[] cols = Physics.OverlapSphere(transform.position, 150);
		List<Collider> relevantCols = new List<Collider> ();
		Vector3 nearest = transform.position;
		foreach (Collider col in cols)
			if (col.gameObject.tag == "Enemy")
				relevantCols.Add(col);
		if (relevantCols.Count <= 0)
			return nearest;
		nearest = relevantCols [0].gameObject.transform.position;
		float distToNearest = Vector3.Distance (transform.position, nearest);
		foreach (Collider col in relevantCols) {
			float distToCurrent = Vector3.Distance(transform.position, col.gameObject.transform.position);
			if (distToCurrent < distToNearest) {
				nearest = col.gameObject.transform.position;
				distToNearest = distToCurrent;
			}
		}
		return nearest;
	}

	void OnCollisionEnter(Collision collision) {
		if (nocollide)
			return;
		//Debug.Log ("in collsion function");
		if (!invincible) {
			if (collision.collider.tag == "Obstacle")
				hitpoints -= obstacleDamage;
			if (collision.collider.tag == "Enemy")
				hitpoints -= enemyDamage;
		}
	}

	void OnTriggerEnter(Collider other) {
		if (other.collider.tag == "Score Powerup") {
			GameManagerScript.score += 1;
			Destroy(other.collider.gameObject);
			gameManagerRef.GetComponent<GameManagerScript>().createScorePowerup_Delayed();
		}
	}
	
	void checkDead() {
		if (hitpoints <= 0)
			resetGame ();
	}

	void resetGame() {
		Application.LoadLevel(0);
	}
}
