using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyScript : MonoBehaviour {

	const int WANDER = 0;
	const int PURSUE = 1;
	const int SEARCH = 2;

	//float speed = 35.0f;
	float speed = 50.0f;
	float maxSpeed = 55.0f;
	public Vector3 destination;
	public Vector3 direction;
	//float turnPenalty = 0.03f;
	//float turnPenalty = 0.5f;
	float turnPenalty = 0.2f;

	float sightRange = 100.0f;
	float fov = 45.0f; // half of FOV, actually

	float avoidanceRange; // range at which obstacles and other agents are detected and avoided
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

	float obstacleAvoidanceWeight = 1.5f; // for obstacles
	float boidAvoidanceWeight = 4.5f; // for other boids/agents
	float cohesionWeight = 1.0f;
	//float cohesionWeight = 0.0f;
	float alignmentWeight = 2.0f;

	float destinationWeight = 1f;

	public int state;
	bool playerInSight;
	Vector3 lastKnownPlayerPos;
	Vector3 prevLastKnownPlayerPos;
	Vector3 lastKnownPlayerVelocity;

	bool omniscient;
	GameObject player;

	// Use this for initialization
	void Start () {

		neighbors = new List<GameObject> ();

		direction = Vector3.forward;
		destination = Vector3.forward * 1000;

		player = GameObject.FindGameObjectWithTag ("Player");
		omniscient = false;

		avoidanceRange = transform.localScale.magnitude * 1.5f;
		//avoidanceRange = gameObject.GetComponent<SphereCollider>().bounds.extents.magnitude * 3;
		closeInDist = avoidanceRange * 8;
		setCohesionRangeShort ();

		lastKnownPlayerPos = destination;
		prevLastKnownPlayerPos = destination;
		lastKnownPlayerVelocity = Vector3.zero;

		state = WANDER;
		changeColorBasedOnState ();
		playerInSight = false;
		InvokeRepeating ("changeDestination", 5, 5);
	}
	
	// Update is called once per frame
	void Update () {

		/*
		if (state == PURSUE || state == SEARCH) {
			Debug.DrawLine (transform.position, lastKnownPlayerPos, Color.magenta);
			Debug.DrawLine (lastKnownPlayerPos, lastKnownPlayerPos + lastKnownPlayerVelocity, Color.red);

		}
		*/

		neighbors.Clear ();
		getNeighbors (); // form flock from other nearby boids

		if (neighbors.Count <= 0)
			setCohesionRangeLong ();
		else
			setCohesionRangeShort();

		dampenRigidbodyForces ();
		//Debug.Log ("state is " + state);

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

		visionCheck ();

		/*
		if (playerInSight)
			Debug.DrawLine(transform.position, lastKnownPlayerPos, Color.green);
			*/

		if (state == PURSUE) {
			destination = lastKnownPlayerPos;
		}

		Debug.DrawLine (transform.position, destination, Color.yellow);

		/*
		Vector3 newDirection = destination - transform.position;
		newDirection = newDirection.normalized * 0.03;
		direction += newDirection;
		direction.Normalize ();
		direction += obstacleCheck (); // avoid obstacles
		direction += getCohesionVec ();
		direction += getAlignmentVec ();
		*/

		/*
		float multiplier = 1;
		if (state == PURSUE || state == SEARCH)
				multiplier = 9;
		destinationWeight = multiplier;
		*/

		Vector3 newDirection = destination - transform.position;
		newDirection = newDirection.normalized;
		newDirection.Normalize();

		Vector3 directVectorToDest = newDirection;

		newDirection *= destinationWeight;
		newDirection += obstacleCheck (); // avoid obstacles
		newDirection += getCohesionVec ();
		newDirection += getAlignmentVec ();

		//Debug.Log ("alignment vec is " + getAlignmentVec () + ", cohesionVec is " + getCohesionVec () +
		//           ", headed for destination " + destination + " and player is at " + player.transform.position);

		newDirection.Normalize ();

		if (Vector3.Distance (transform.position, destination) < 5 && (state == PURSUE || state == SEARCH))
			newDirection = directVectorToDest;

		direction.Normalize ();

		direction += newDirection * turnPenalty;

		//direction = 0.9f * direction + newDirection;
		//direction = newDirection * turnPenalty;

		Vector3 newPos = direction * speed;
		if (newPos.magnitude > maxSpeed)
			newPos = Vector3.ClampMagnitude (newPos, maxSpeed);

		rigidbody.MovePosition (transform.position + (newPos * Time.deltaTime));

		transform.LookAt (transform.position + direction);

		changeColorBasedOnState ();

		//Debug.Log ("enemy at " + transform.position);
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
					lastKnownPlayerPos = objPos;
					lastKnownPlayerVelocity = lastKnownPlayerPos - prevLastKnownPlayerPos;
					if (lastKnownPlayerVelocity.magnitude > 50)
						lastKnownPlayerVelocity = Vector3.ClampMagnitude(lastKnownPlayerVelocity, 50);
					prevLastKnownPlayerPos = lastKnownPlayerPos;
					playerInSight = true;
					if (state == WANDER)
						CancelInvoke("changeDestination");
					if (state == SEARCH) {
						CancelInvoke("finishSearching");
					}
					state = PURSUE;
					return;
				}
			}
			playerInSight = false;
			if (state == PURSUE) {
				state = SEARCH;
				Invoke("finishSearching", 5);
			}
			if (state == SEARCH) {
				destination += lastKnownPlayerVelocity;
				//Debug.Log("searching for player at " + destination);
			}
			/*
			if (state != WANDER) {
				InvokeRepeating ("changeDestination", 5, 5);
				state = WANDER;
			}
			*/
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
		Collider[] cols = Physics.OverlapSphere(transform.position, avoidanceRange);
		Vector3 avoidanceVec = Vector3.zero;
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Obstacle") {
				Vector3 vecFromObj = transform.position - col.gameObject.transform.position;
				float range = vecFromObj.magnitude;
				vecFromObj.Normalize();
				avoidanceVec += vecFromObj * obstacleAvoidanceWeight * (1 / Mathf.Pow(range, 2));
			}
			else if (col.gameObject.tag == "Enemy" && col.gameObject != gameObject) {
				Vector3 vecFromObj = transform.position - col.gameObject.transform.position;
				float range = vecFromObj.magnitude;
				vecFromObj.Normalize();
				avoidanceVec += vecFromObj * boidAvoidanceWeight * (1 / Mathf.Pow(range, 2));
			}
		}
		//Debug.Log ("avoidance vec is: " + avoidanceVec.magnitude);
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
		if (Vector3.Distance (transform.position, localCentroid) < avoidanceRange * 4f)
			return Vector3.zero;
		Vector3 vecToLocalCent = localCentroid - transform.position;
		return vecToLocalCent * cohesionWeight * multiplier;
	}

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
		cohesionRange = avoidanceRange * defaultCohesionRangeMult;
	}

	void setCohesionRangeLong() {
		cohesionRange = avoidanceRange * longCohesionRangeMult;
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
		createPt *= Random.Range(400, 600);

		destination = transform.position + createPt;
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
