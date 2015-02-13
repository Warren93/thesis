using UnityEngine;
using System.Collections;

public class PlayerScript : MonoBehaviour {

	bool nocollide = false;
	float hitpoints = 100.0f;

	Vector3 prevDirection;
	Camera mouseLookCam;
	Camera mainCam;

	float mouseLookY_Rotation;
	float mouseLookX_Rotation;
	float mouseLookSensitivity;

	float defaultForwardSpeed = 40.0f;
	public float forwardSpeed;
	public float sidewaysSpeed;

	float boostCharge = 100.0f;
	float boostDecrement = 30.0f;
	float boostIncrement;

	public float mouseX_AxisSensitivity;
	public float mouseY_AxisSensitivity;
	public float rollRate;

	// Use this for initialization
	void Start () {

		boostIncrement = boostDecrement * 0.5f;

		mouseLookY_Rotation = 0;
		mouseLookX_Rotation = 0;
		mouseLookSensitivity = 200;

		mouseLookCam = GameObject.Find("MouseLookCam").camera;
		mainCam = GameObject.Find ("Main Camera").camera;
		sidewaysSpeed = defaultForwardSpeed * 0.7f;
		mouseY_AxisSensitivity = 100.0f;
		mouseX_AxisSensitivity = mouseY_AxisSensitivity * 0.4f;
		rollRate = 75.0f;
	}
	
	// Update is called once per frame
	void Update () {

		dampenRigidbodyForces ();
		//Debug.Log ("mouse look cam is at " + mouseLookCam.transform.position);

		if (Input.GetMouseButtonDown (2)) {
			mainCam.enabled = false;
			mainCam.GetComponent<AudioListener>().enabled = false;
			mouseLookCam.enabled = true;
			mouseLookCam.GetComponent<AudioListener>().enabled = true;
		}
		else if (Input.GetMouseButtonUp (2)) {
			mainCam.enabled = true;
			mainCam.GetComponent<AudioListener>().enabled = true;
			mouseLookCam.enabled = false;
			mouseLookCam.GetComponent<AudioListener>().enabled = false;
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
		if (Input.GetMouseButton(2)) {
			mouseLookCam.transform.position = mainCam.transform.position;
			mouseLookCam.transform.rotation = mainCam.transform.rotation;
			mouseLookY_Rotation += Time.deltaTime * deltaMouseX * mouseLookSensitivity;
			mouseLookX_Rotation += Time.deltaTime * deltaMouseY * mouseLookSensitivity;
			mouseLookCam.transform.RotateAround(transform.position, transform.right, mouseLookX_Rotation);
			mouseLookCam.transform.RotateAround(transform.position, transform.up, mouseLookY_Rotation);
		}
		// movement stuff
		if((deltaMouseX != 0 || deltaMouseY != 0) && !Input.GetMouseButton(2)){
			//Debug.Log("mouse moved");
			transform.RotateAround(transform.position, transform.up, Time.deltaTime * deltaMouseX * mouseX_AxisSensitivity);
			transform.RotateAround(transform.position, transform.right, Time.deltaTime * -1 * deltaMouseY * mouseY_AxisSensitivity);
		}

		forwardSpeed = defaultForwardSpeed;

		//Debug.Log ("forward speed is " + forwardSpeed);

		// accelerate (use boost)
		if (Input.GetKey (KeyCode.LeftShift) && boostCharge > 0) {
			forwardSpeed = defaultForwardSpeed * 2f;
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

		Debug.Log ("boost is at " + boostCharge + ", hitpoints at " + hitpoints);

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

	void OnCollisionEnter(Collision collision) {
		if (nocollide)
			return;
		//Debug.Log ("in collsion function");
		if (collision.collider.tag == "Obstacle" || collision.collider.tag == "Enemy") {
			hitpoints -= 20.0f;
		}
	}

	void checkDead() {
		if (hitpoints <= 0)
			endGame ();
	}

	void endGame() {
		Destroy (gameObject);
		Application.Quit();
		if ((Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor)
		    && UnityEditor.EditorApplication.isPlaying == true) {
			UnityEditor.EditorApplication.isPlaying = false;
		}
	}
}
