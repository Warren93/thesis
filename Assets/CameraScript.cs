using UnityEngine;
using System.Collections;

public class CameraScript : MonoBehaviour {

	float damping = 8.0f;
	bool smooth = true;
	Vector3 target;
	GameObject player;
	float camAltAbovePlayer;
	float camDistBehindPlayer;
	Vector3 vecFromPlayer;
	float distFromPlayer;

	float prev_y_angle = 0;
	float prev_x_angle = 0;

	// Use this for initialization
	void Start () {
		player = GameObject.FindGameObjectWithTag("Player");
		//transform.parent = player.transform;
		camAltAbovePlayer = transform.position.y - player.transform.position.y;
		camDistBehindPlayer = transform.position.z - player.transform.position.z;
		vecFromPlayer = transform.position - player.transform.position;
		distFromPlayer = vecFromPlayer.magnitude;
	}
	
	// Update is called once per frame
	void Update () {
		if (!player)
			return;
		target = player.transform.position + (player.transform.up * camAltAbovePlayer);
		//transform.position = target + (player.transform.forward * camDistBehindPlayer);



		//moveCam ();
	}
	
	void LateUpdate () {

		if (!player)
			return;

		if (Input.GetKey(KeyCode.W)
		    || Input.GetKey(KeyCode.A)
		    || Input.GetKey(KeyCode.S)
		    || Input.GetKey(KeyCode.D)) {
			transform.position = player.transform.position + player.transform.forward * camDistBehindPlayer;
			transform.position += player.transform.up * camAltAbovePlayer;
			
			if (Input.GetKey(KeyCode.A))
				if (prev_y_angle > -10)
					prev_y_angle -= 1;
			if (Input.GetKey(KeyCode.D))
				if (prev_y_angle < 10)
					prev_y_angle += 1;
			if (Input.GetKey(KeyCode.W))
				if (prev_y_angle < 10)
					prev_x_angle += 1;
			if (Input.GetKey(KeyCode.S))
				if (prev_x_angle > -5)
					prev_x_angle -= 1;
		}

		//moveCam ();

		//interpolateCamPosition ();
		//transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer);

		// Look at and dampen the rotation
		var rotation = Quaternion.LookRotation(target - transform.position, player.transform.up);
		transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * damping);
		if (!smooth) {
			// Just lookat
			transform.LookAt(target, player.transform.up);
			return;
		}


		//float y_angle_diff = transform.rotation.eulerAngles.y - player.transform.rotation.eulerAngles.y;

		float new_y_angle = Input.GetAxis ("Mouse X");
		float new_x_angle = Input.GetAxis ("Mouse Y");

		float lerpStep = 0.75f;
		//lerpStep = Mathf.Max (Mathf.Abs (prev_y_angle - new_y_angle), Mathf.Abs (prev_x_angle - new_x_angle));

		float y_angle = Mathf.Lerp (prev_y_angle, new_y_angle, lerpStep);
		if (y_angle > 10)
			y_angle = 10;
		else if (y_angle < -10)
			y_angle = -10;
		//Debug.Log ("y angle is " + y_angle);

		float x_angle = Mathf.Lerp (prev_x_angle, new_x_angle, lerpStep);
		if (x_angle > 10)
			x_angle = 10;
		else if (x_angle < -10)
			x_angle = -10;

		transform.position = player.transform.position + player.transform.forward * camDistBehindPlayer;
		transform.position += player.transform.up * camAltAbovePlayer;
		transform.position += player.transform.right * y_angle * 10 * Time.deltaTime;
		transform.position += player.transform.up * x_angle * 10 * Time.deltaTime;

		if (!(Input.GetKey(KeyCode.W)
		      || Input.GetKey(KeyCode.A)
		      || Input.GetKey(KeyCode.S)
		      || Input.GetKey(KeyCode.D))) {
			prev_y_angle = new_y_angle;
			prev_x_angle = new_x_angle;
		}
		//transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer) + player.transform.right * y_angle_diff * 0.1f;


	}

	void FixedUpdate() {
		//interpolateCamPosition ();
		//transform.position = transform.position * 0.99f + target * 0.01f;
	}

	void interpolateCamPosition() {
		Vector3 wanted = player.transform.rotation * vecFromPlayer;
		//Vector3 wanted = vecFromPlayer;
		Debug.DrawRay (player.transform.position, wanted * 5, Color.red);
		Vector3 current = transform.position - player.transform.position;
		//current -= player.transform.up * camAltAbovePlayer;
		Debug.DrawRay (player.transform.position, current * 5, Color.green);

		//Debug.Log ("angle between current and wanted is " + Vector3.Angle (current + player.transform.up * camAltAbovePlayer, wanted));
		//Debug.Log ("angle between current and wanted is " + Vector3.Angle (current, wanted));
		float angle = Vector3.Angle (current, wanted);
		float step = Time.deltaTime;
		//step = 0.01f;
		Debug.Log ("angle is " + angle + ", step is " + step);

		if (angle > 0)
		transform.position = player.transform.position
			+ Vector3.Slerp (current, wanted, step).normalized * distFromPlayer;
		else
			transform.position = player.transform.position+ wanted;

//		Vector3 velocity = Vector3.zero;
//		float smoothTime = 0.3f;
//		transform.position = player.transform.position
//			+ Vector3.SmoothDamp(current, wanted, ref velocity, smoothTime, 0.2f).normalized * distFromPlayer;



		//transform.position += player.transform.up * camAltAbovePlayer;

		//transform.position = player.transform.position+ wanted;
	}

	void moveCam() {
		transform.position = player.transform.position + Vector3.ClampMagnitude(transform.position - player.transform.position, 8);
	}

	/*
	void LateUpdate () {
		var smooth = 2.0f;
		var tiltAngle = 10.0f;
		float x_axis = Input.GetAxis("Mouse X") * tiltAngle;
		float y_axis = Input.GetAxis("Mouse Y") * tiltAngle;
		//Debug.Log ("x axis: " + x_axis);
		//Quaternion targetRot = Quaternion.Euler (tiltAroundX, 0f, tiltAroundZ);
		//transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer) + Vector3.right * tiltAroundX;
		float scaled_x = x_axis * (1.0f / 30);
		float scaled_y = y_axis * (1.0f / 30);
		Debug.Log ("scaled x axis is " + scaled_x);
		if (x_axis > 0)
			transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer) + player.transform.right * scaled_x;
		else if (x_axis < 0)
			transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer) + player.transform.right * scaled_x;
		else
			transform.position = player.transform.position + (player.transform.rotation * vecFromPlayer);
		//Quaternion targetRot = Quaternion.Euler (0f, tiltAroundZ, 0f);
		
		//transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smooth);
	}
*/

}
