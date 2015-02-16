﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyScript : MonoBehaviour {

	public const int WANDER = 0;
	public const int PURSUE = 1;
	public const int SEARCH = 2;

	public GameObject gameManger; // reference to the game manager object

	float searchTime = 8;

	//float speed = 35.0f;
	float defaultSpeed = 45.0f;
	float speed;
	float maxSpeed;
	public Vector3 destination;
	public Vector3 direction;
	//float turnPenalty = 0.03f;
	//float turnPenalty = 0.5f;
	float turnPenalty = 0.2f;

	//float sightRange = 100.0f;
	float sightRange = 150.0f;
	float fov = 45.0f; // half of FOV, actually

	float obstacleAvoidanceRange; // range at which obstacles and other agents are detected and avoided
	float agentAvoidanceRange;
	float cohesionRange;
	float closeInDist;

	float defaultCohesionRangeMult = 10;
	float longCohesionRangeMult = 40;

	List<GameObject> neighbors;

	/*
	float obstacleAvoidanceWeight = 1.5f; // for obstacles
	float boidAvoidanceWeight = 4.5f; // for other boids/agents
	float cohesionWeight = 0.05f;
	float alignmentWeight = 2.0f;
	*/

	float defaultObstacleAvoidanceWeight = 4.5f;
	float defaultAgentAvoidanceWeight = 7.5f;
	float defaultCohesionWeight = 0.7f;
	float defaultAlignmentWeight = 2.0f;
	float defaultDestinationWeight = 1f;

	float obstacleAvoidanceWeight;
	float agentAvoidanceWeight;
	float cohesionWeight;
	float alignmentWeight;
	float destinationWeight;

	public int state;

	bool playerInSight;
	Vector3 lastKnownPlayerPos; // the last place the player was seen
	Vector3 prevLastKnownPlayerPos; // the previous last place the player was seen
	Vector3 lastKnownPlayerVelocity; // the last known player velocity
	public Vector3 playerPosEstimate; // estimate of where the player currently is (useful if player not in sight)

	bool omniscient;
	GameObject player;

	public Vector3 obstacleVec;
	public Vector3 agentVec;
	public Vector3 cohesionVec;
	public Vector3 alignmentVec;

	// Use this for initialization
	void Start () {

		setDefaultWeights ();

		obstacleVec = agentVec = cohesionVec = alignmentVec = Vector3.zero;

		neighbors = new List<GameObject> ();

		direction = Vector3.forward;
		destination = Vector3.forward * 1000;

		player = GameObject.FindGameObjectWithTag ("Player");
		omniscient = false;

		obstacleAvoidanceRange = transform.localScale.magnitude * 1.5f;
		//avoidanceRange = gameObject.GetComponent<SphereCollider>().bounds.extents.magnitude * 3;
		agentAvoidanceRange = obstacleAvoidanceRange * 2;
		//closeInDist = obstacleAvoidanceRange * 8;
		closeInDist = 15;
		//closeInDist = 50;
		setCohesionRangeShort ();

		lastKnownPlayerPos = destination;
		playerPosEstimate = destination;
		prevLastKnownPlayerPos = destination;
		lastKnownPlayerVelocity = Vector3.zero;

		state = WANDER;
		changeColorBasedOnState ();
		playerInSight = false;
		InvokeRepeating ("changeDestination", 5, 5);
	}
	
	// Update is called once per frame
	void Update () {

		speed = defaultSpeed;
		maxSpeed = defaultSpeed * 1.1f;

		// FIND PACK (or flock, to use the boid terminology)
		neighbors.Clear ();
		getNeighbors (); // form flock from other nearby boids

		// IF LONE WOLF, EXPAND COHESION RANGE TO TRY TO FIND/FORM A PACK
		if (neighbors.Count <= 0)
			setCohesionRangeLong ();
		else if (neighbors.Count > 8)
			cohesionRange = cohesionRange * 0.5f;
		else
			setCohesionRangeShort();

		// RECOVER FROM ANY COLLISION(S)
		dampenRigidbodyForces ();
		//Debug.Log ("state is " + state);

		// IF PLAYER PRESSES 'O' SET NPCs OMNISCIENT (automatically know where player is) FOR DEBUGGING
		if (Input.GetKey (KeyCode.O)) {
			if (omniscient == true) {
				omniscient = false;
				//Debug.Log("NPCs non-omniscient");
			}
			else {
				omniscient = true;
				//Debug.Log("NPCs omniscient");
			}
		}

		// CHECK IF PLAYER IN SIGHT
		visionCheck ();

		// IF PLAYER IN SIGHT, PURSUE PLAYER
		if (playerInSight && state != PURSUE)
			changeStateTo (PURSUE);
		// BEGIN SEARCHING IF PLAYER DISAPPEARED WHILE STATE WAS 'PURSUE'
		else if (!playerInSight && state == PURSUE)
			changeStateTo(SEARCH);

		// UPDATE ESTIMATE OF WHERE PLAYER IS
		updatePlayerPosEstimate ();

		// IF NOT WANDERING, GO TO WHERE PLAYER IS THOUGHT TO BE
		if (state != WANDER)
			destination = playerPosEstimate;

		//Debug.DrawLine (transform.position, destination, Color.yellow);

		Vector3 newDirection = destination - transform.position;
		newDirection = newDirection.normalized;
		newDirection.Normalize();

		Vector3 directVectorToDest = newDirection;

		if (state == PURSUE || state == SEARCH)
			destinationWeight = 5;
		else
			destinationWeight = 1;

		newDirection *= destinationWeight;

		/*
		newDirection += obstacleCheck ();
		newDirection += agentCheck ();
		newDirection += getCohesionVec ();
		newDirection += getAlignmentVec ();
		*/

		obstacleVec = obstacleCheck ();
		agentVec = agentCheck ();
		cohesionVec = getCohesionVec ();
		alignmentVec = getAlignmentVec ();

		newDirection += obstacleVec;
		newDirection += agentVec;
		newDirection += cohesionVec;
		newDirection += alignmentVec;

		if (state == SEARCH) {
			Debug.DrawLine(transform.position, playerPosEstimate, Color.red);
			Debug.Log ("alignment vec is " + getAlignmentVec () + ", cohesion vec is " + getCohesionVec ()
			           + ", agent avoid vec is " + agentVec +
		           		", headed for destination " + destination + " and player is at " + player.transform.position);
		}

		newDirection.Normalize ();

		if (Vector3.Distance (transform.position, destination) < closeInDist && (state == PURSUE || state == SEARCH)) {
			newDirection = directVectorToDest;
			maxSpeed = lastKnownPlayerVelocity.magnitude / Time.deltaTime;
			maxSpeed *= 1.1f;
		}

		direction.Normalize ();

		direction += newDirection * turnPenalty;

		Vector3 newPos = direction * speed;

		// DON'T TRAVEL AT MAX SPEED IF NOT ATTACKING PLAYER
		if (state == WANDER && newPos.magnitude > speed)
			newPos = Vector3.ClampMagnitude (newPos, speed);

		if (newPos.magnitude > maxSpeed)
			newPos = Vector3.ClampMagnitude (newPos, maxSpeed);

		rigidbody.MovePosition (transform.position + (newPos * Time.deltaTime));

		transform.LookAt (transform.position + direction);

		changeColorBasedOnState ();
	}

	void changeStateTo(int newState) {
		if (state == newState)
			return;

		if (state == SEARCH && newState != SEARCH) {
			CancelInvoke("finishSearching");
			setDefaultWeights();
		}

		if (state == WANDER && newState != WANDER)
			CancelInvoke("changeDestination");

		if (newState == PURSUE)
			destination = lastKnownPlayerPos;
		else if (newState == SEARCH) {
			spreadOut();
			Invoke("finishSearching", searchTime);
		}

		state = newState;
	}

	void spreadOut() {
		agentAvoidanceRange = 5000;
		agentAvoidanceWeight = 100;
		cohesionWeight = 0.0f;
		alignmentWeight = 0.0f;
		destinationWeight = 100;
	}

	void setDefaultWeights() {
		obstacleAvoidanceWeight = defaultObstacleAvoidanceWeight;
		agentAvoidanceWeight = defaultAgentAvoidanceWeight;
		cohesionWeight = defaultCohesionWeight;
		alignmentWeight = defaultAlignmentWeight;
		destinationWeight = defaultDestinationWeight;
	}

	void updatePlayerPosEstimate() {
		if (playerInSight)
			playerPosEstimate = lastKnownPlayerPos;
		else {
			playerPosEstimate += lastKnownPlayerVelocity;
		}
	}

	void updateMemoryOfPlayerPosTo(Vector3 position) {
		lastKnownPlayerPos = position;
		lastKnownPlayerVelocity = lastKnownPlayerPos - prevLastKnownPlayerPos;
		if (lastKnownPlayerVelocity.magnitude > 100)
			lastKnownPlayerVelocity = Vector3.ClampMagnitude(lastKnownPlayerVelocity, 100);
		prevLastKnownPlayerPos = lastKnownPlayerPos;
	}

	void visionCheck() {
		if (omniscient) {
			if (state == WANDER)
				CancelInvoke("changeDestination");
			lastKnownPlayerPos = player.transform.position;
			state = PURSUE;
			playerInSight = true;
			return;
		}
		// check to see if player in sight
		Collider[] cols = Physics.OverlapSphere(transform.position, sightRange);
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Player") {
				Vector3 objPos = col.gameObject.transform.position;
				Vector3 vecToPlayer = objPos - transform.position;
				float angle = Mathf.Abs(Vector3.Angle(direction, vecToPlayer));
				if (angle <= fov && clearLOS(gameObject, col.gameObject, sightRange)) {
					updateMemoryOfPlayerPosTo(col.gameObject.transform.position);
					playerInSight = true;
					return;
				}
			}
			playerInSight = false;
		}
	}

	bool clearLOS(GameObject obj1, GameObject obj2, float range) {
		RaycastHit[] hits;
		Vector3 rayDirection = obj2.transform.position - obj1.transform.position;
		hits = Physics.RaycastAll(obj1.transform.position, rayDirection, range);
		if (hits.Length <= 0)
			return false;
		GameObject closest = hits [0].collider.gameObject;
		float distToClosest = Vector3.Distance(obj1.transform.position, closest.transform.position);
		foreach (RaycastHit hit in hits) {
			GameObject current = hit.collider.gameObject;
			if (current == obj1)
				continue;
			float distToCurrent = Vector3.Distance(obj1.transform.position, current.transform.position);
			if (distToCurrent < distToClosest) {
				closest = current;
				distToClosest = distToCurrent;
			}
		}
		if (closest == obj2)
			return true;
		else
			return false;
	}

	void finishSearching() {
		if (state != WANDER) {
			InvokeRepeating ("changeDestination", 5, 5);
			state = WANDER;
		}
	}

	Vector3 obstacleCheck() {
		// check for obstacles and return vector away from them
		Collider[] cols = Physics.OverlapSphere(transform.position, obstacleAvoidanceRange);
		Vector3 avoidanceVec = Vector3.zero;
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Obstacle") {
				Vector3 vecFromObj = transform.position - col.gameObject.transform.position;
				float range = vecFromObj.magnitude;
				vecFromObj.Normalize();
				//avoidanceVec += vecFromObj * obstacleAvoidanceWeight * (1 / Mathf.Pow(range, 2));
				avoidanceVec += vecFromObj * obstacleAvoidanceWeight * (1 / range);
			}
		}
		return avoidanceVec;
	}

	Vector3 agentCheck() {
		// check for other agents and return vector away from them
		Collider[] cols = Physics.OverlapSphere(transform.position, agentAvoidanceRange);
		Vector3 avoidanceVec = Vector3.zero;
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Obstacle") {
				Vector3 vecFromObj = transform.position - col.gameObject.transform.position;
				float range = vecFromObj.magnitude;
				vecFromObj.Normalize();
				//avoidanceVec += vecFromObj * agentAvoidanceWeight * (1 / Mathf.Pow(range, 2));
				avoidanceVec += vecFromObj * agentAvoidanceWeight * (1 / range);
			}
		}
		return avoidanceVec;
	}

	void getNeighbors() {
		Collider[] cols = Physics.OverlapSphere(transform.position, cohesionRange);
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Enemy" && col.gameObject != gameObject) {
				neighbors.Add(col.gameObject);
			}
		}
	}

	Vector3 getCohesionVec() {
		float multiplier = 1;
		if (state == PURSUE || state == SEARCH)
			multiplier = 0.2f;
		Vector3 localCentroid = transform.position;
		float flockSize = neighbors.Count + 1;
		foreach (GameObject obj in neighbors) {
			localCentroid += obj.transform.position;
		}
		localCentroid /= flockSize;
		// if already close enough to flock centroid, don't bother with cohesion
		if (Vector3.Distance (transform.position, localCentroid) < agentAvoidanceRange * 4f)
			return Vector3.zero;
		Vector3 vecToLocalCent = localCentroid - transform.position;
		return vecToLocalCent * cohesionWeight * multiplier;
	}

	/*
	Vector3 getAlignmentVec () {
		float multiplier = 1;
		if (state == PURSUE || state == SEARCH)
			multiplier = 0.1f;
		// get average destination of flock
		Vector3 avgDest = destination;
		float flockSize = neighbors.Count + 1;
		foreach (GameObject obj in neighbors) {
			EnemyScript currentObj = obj.GetComponent<EnemyScript>();
			avgDest += currentObj.destination;
		}
		avgDest /= flockSize;
		// get vector to average destination
		Vector3 vecToAvgDest = avgDest - transform.position;
		return vecToAvgDest.normalized * alignmentWeight * multiplier;
	}
	*/

	Vector3 getAlignmentVec () {
		float multiplier = 1;
		// get average destination of flock
		Vector3 avgDest = destination;
		float flockSize = neighbors.Count + 1;
		foreach (GameObject obj in neighbors) {
			EnemyScript currentObj = obj.GetComponent<EnemyScript>();
			avgDest += currentObj.destination;
		}
		if (state == PURSUE || state == SEARCH)
			multiplier = 3;
		avgDest += destination * multiplier;
		avgDest /= flockSize;
		// get vector to average destination
		Vector3 vecToAvgDest = avgDest - transform.position;
		return vecToAvgDest.normalized * alignmentWeight;
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

	void setCohesionRangeShort() {
		cohesionRange = agentAvoidanceRange * defaultCohesionRangeMult;
	}

	void setCohesionRangeLong() {
		cohesionRange = agentAvoidanceRange * longCohesionRangeMult;
	}

	void changeDestination() {
		if (state != WANDER) {
			CancelInvoke("changeDestination");
			return;
		}

		float f1, f2, f3;
		f1 = Random.Range(-10, 10);
		f2 = Random.Range(-10, 10);
		f3 = Random.Range(-10, 10);
		Vector3 createPt = new Vector3(f1, f2, f3);
		createPt.Normalize();
		//createPt *= Random.Range(800, 1000);
		//destination = transform.position + createPt;

		createPt *= Random.Range(GameManagerScript.creationRadius * 0.3f, GameManagerScript.creationRadius * 1.3f);
		destination = createPt;
	}

	void changeColorBasedOnState() {
		if (state == WANDER)
			renderer.material.color = Color.green;
		else if (state == PURSUE)
			renderer.material.color = Color.red;
		else if (state == SEARCH)
			renderer.material.color = Color.yellow;
		else
			renderer.material.color = Color.white; // should never get here
		}
}
