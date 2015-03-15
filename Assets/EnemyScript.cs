using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyScript : MonoBehaviour {

	public const int WANDER = 0;
	public const int PURSUE = 1;
	public const int SEARCH = 2;

	public GameObject gameManger; // reference to the game manager object

	float searchTime = 8;

	float defaultSpeed = 65.0f;
	//float defaultSpeed = 20.0f;
	float maxSpeed;
	public Vector3 destination;
	public Vector3 direction;
	Vector3 facingDirection;
	float speedScaleFac = 0.25f;
	//float speedScaleFac = 0.15f;
	float energyLevel = 100;
	//float energyRegenRate = 5;
	float energyRegenRate = 20;

	//float sightRange = 100.0f;
	float sightRange = 150.0f;
	float fov = 45.0f; // half of FOV, actually
	float smellRange;

	float obstacleAvoidanceRange; // range at which obstacles and other agents are detected and avoided
	float agentAvoidanceRange;
	float cohesionRange;
	float closeInDist;

	float defaultCohesionRangeMult = 10;
	float longCohesionRangeMult = 30;

	List<GameObject> neighbors;
	List<GameObject> closeNeighbors;
	float neighborRefreshRate = 0.2f; // frequency in seconds at which neighboring NPCs are detected
	float neighborCheckTargetRate = 1.5f; //0.33f; // frequency in seconds that this agent checks if any neighbor has found the player

	float defaultObstacleAvoidanceWeight = 20f;
	float defaultAgentAvoidanceWeight = 1.5f;
	float defaultCohesionWeight = 6.0f;
	float defaultAlignmentWeight = 4.0f;
	float defaultDestinationWeight = 1f;

	float obstacleAvoidanceWeight;
	float agentAvoidanceWeight;
	float cohesionWeight;
	float alignmentWeight;
	float destinationWeight;

	float smellWeight = 50000;
	float smellMultiplier = 1;
	float standoffWeight;

	float prevSmellLevel = 0;
	float smellLevel = 0;

	public int state;

	bool playerInSight;
	Vector3 lastKnownPlayerPos; // the last place the player was seen
	Vector3 prevLastKnownPlayerPos; // the previous last place the player was seen
	Vector3 lastKnownPlayerVelocity; // the last known player velocity
	public Vector3 playerPosEstimate; // estimate of where the player currently is (useful if player not in sight)

	bool omniscient;
	bool playerInvisible;
	bool flocking = true;
	GameObject player;

	public Vector3 obstacleVec;
	public Vector3 agentVec;
	public Vector3 cohesionVec;
	public Vector3 alignmentVec;

	public Vector3 standoffVec;

	TrailRenderer trail;

	// Use this for initialization
	void Start () {

		//GetComponent<SphereCollider> ().enabled = false;
		//GetComponent<CapsuleCollider> ().enabled = false;

		setDefaultWeights ();

		smellRange = sightRange * 2.5f;

		obstacleVec = agentVec = cohesionVec = alignmentVec = Vector3.zero;

		neighbors = new List<GameObject> ();
		closeNeighbors = new List<GameObject> ();

		direction = Vector3.forward;
		destination = Vector3.forward * 1000;
		facingDirection = direction;

		player = GameObject.FindGameObjectWithTag ("Player");
		omniscient = false;
		playerInvisible = false;

		trail = GetComponent<TrailRenderer> ();

		obstacleAvoidanceRange = transform.localScale.magnitude * 1.5f;
		//avoidanceRange = gameObject.GetComponent<SphereCollider>().bounds.extents.magnitude * 3;
		agentAvoidanceRange = obstacleAvoidanceRange * 0.7f;
		//closeInDist = obstacleAvoidanceRange * 8;
		//closeInDist = 15;
		closeInDist = 30;
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
		if (flocking) {
			InvokeRepeating ("getNeighbors", Random.Range (0, neighborRefreshRate), neighborRefreshRate);
			InvokeRepeating ("checkNeighborFoundPlayer", Random.Range (0, neighborCheckTargetRate), neighborCheckTargetRate);
		}
	}
	
	// Update is called once per frame
	void Update () {

		//Debug.DrawRay (player.transform.position + player.transform.up * 5, player.transform.right * energyLevel, Color.magenta);
		//Debug.Log ("energy level: " + energyLevel);

		maxSpeed = defaultSpeed * 1.3f;

		// RECOVER FROM ANY COLLISION(S)
		dampenRigidbodyForces ();
		//Debug.Log ("state is " + state);

		// TOGGLE NPC OMNISCIENCE (automatically know where player is) for debugging
		if (Input.GetKeyUp (KeyCode.O)) {
			if (omniscient == true) {
				omniscient = false;
				//Debug.Log("NPCs non-omniscient");
			}
			else {
				omniscient = true;
				//Debug.Log("NPCs omniscient");
			}
		}

		// TOGGLE PLAYER INVISIBILITY (also for debugging purposes)
		if (Input.GetKeyUp (KeyCode.I)) {
			if (playerInvisible == true)
				playerInvisible = false;
			else
				playerInvisible = true;
		}

		// CHECK IF PLAYER IN SIGHT
		visionCheck ();

		// IF PLAYER IN SIGHT, PURSUE PLAYER
		if (playerInSight)
			changeStateTo (PURSUE);

		// UPDATE ESTIMATE OF WHERE PLAYER IS
		updatePlayerPosEstimate ();

		// CHANGE FLOCKING VARIABLES BASED ON STATE
		updateFlightParams ();

		Vector3 newDirection = destination - transform.position;
		if (state != PURSUE && newDirection.magnitude > 0)
			newDirection.Normalize();

		newDirection *= destinationWeight;

		if (state == WANDER) {
			getSmellMultiplier();
			newDirection *= smellMultiplier;
		}

		Vector3 directVectorToDest = newDirection;

		obstacleVec = obstacleCheck ();
		agentVec = agentCheck ();
		cohesionVec = getCohesionVec ();
		alignmentVec = getAlignmentVec ();

		standoffVec = Vector3.zero;
		if (flocking && neighbors.Count > 0)
			standoffVec = getStandoffVec ();

		newDirection += obstacleVec;
		newDirection += agentVec;
		newDirection += cohesionVec;
		newDirection += alignmentVec;

		// needed for surrounding
		newDirection += standoffVec;

		if (newDirection.magnitude > 0)
			newDirection.Normalize ();

		float speed = defaultSpeed;

		if (!flocking)
			newDirection = directVectorToDest;

		if (direction.magnitude > 0)
			direction.Normalize ();

		float turnAngle = Vector3.Angle (direction, newDirection);
		direction += newDirection * speedScaleFac;

		Vector3 newPos = direction * speed * (energyLevel / 100.0f);

		if (state == WANDER && newPos.magnitude > defaultSpeed)
			newPos = Vector3.ClampMagnitude (newPos, defaultSpeed);

		if (newPos.magnitude > maxSpeed)
			newPos = Vector3.ClampMagnitude (newPos, maxSpeed);

		//if (closeToPlayer() && flocking)
		//	newPos = Vector3.ClampMagnitude(newPos, lastKnownPlayerVelocity.magnitude * 1.2f);

		rigidbody.MovePosition (transform.position + (newPos * Time.deltaTime));

		if (state == WANDER) {
			facingDirection = direction;
		}
		else {
			facingDirection = destination - transform.position;
			facingDirection.Normalize ();
		}
		transform.LookAt (transform.position + facingDirection);
		/*
		Debug.Log ("current speed is " + newPos.magnitude
		           + ", default is " + defaultSpeed + ", max is " + maxSpeed
		           + ", energy level is " + energyLevel
		           + ", regen rate is " + energyRegenRate * Time.deltaTime + " per frame");
		*/

		adjustEnergyLevel (newPos.magnitude, turnAngle);

		changeColorBasedOnState ();

		/*
		if (state == SEARCH) {
			Debug.DrawRay(transform.position, transform.right * agentAvoidanceRange, Color.red);
			Debug.DrawRay(transform.position + transform.up * 2, transform.right * cohesionRange, Color.green);
		}
		*/
	}

	void updateFlightParams() {

		if (state == WANDER) {
			//checkNeighborFoundPlayer ();
			destinationWeight = 1;
			cohesionWeight = defaultCohesionRangeMult;
			if (neighbors.Count <= 0) {
				setCohesionRangeLong ();
				cohesionRange *= 1.5f;
				cohesionWeight = defaultCohesionWeight * 1.33f;
			}
			// if flock too small, expand cohesion range to try to find/form a pack
			else if (neighbors.Count > 0 && neighbors.Count <= 2) {
				setCohesionRangeLong ();
				cohesionWeight = defaultCohesionWeight * 1.33f;
			}
			// if flock is in ideal size range, set moderate cohesion distance
			else if (neighbors.Count < 7) {
				setCohesionRangeShort();
				cohesionRange = cohesionRange * 1.3f;
			}
			// if flock too big, lower cohesion distance
			else {
				setCohesionRangeShort();
				cohesionRange = cohesionRange * 0.5f;
			}
		}
		else if (state == PURSUE) {
			//Debug.DrawLine(transform.position, playerPosEstimate, Color.yellow);
			cohesionWeight = 0.2f;
			destinationWeight = 10f;
			alignmentWeight = defaultAlignmentWeight * 0.2f;
			destination = playerPosEstimate;
			// if close to player, change paramters for surrounding player
			if (closeToPlayer()) {
				cohesionWeight = 0;
				destinationWeight = 1f;
				standoffWeight = 1f;
				alignmentWeight = 0;
				agentAvoidanceWeight = defaultAgentAvoidanceWeight * 8f;
				// find how many neighbors have surrounded the player
				int numNeighborsInPosition = 0;
				foreach (GameObject neighbor in neighbors)
					if (Vector3.Distance(neighbor.transform.position, player.transform.position) <= closeInDist * 1.2f)
						numNeighborsInPosition++;
				// if 75% of the neighbors have surrounded the player, go in for the kill
				if ((float)numNeighborsInPosition / neighbors.Count >= 0.75 || (neighbors.Count  <= 0)) {
					destinationWeight = 10;
					//Debug.DrawLine(transform.position, player.transform.position, Color.white);
				}
				if (!flocking)
					setDefaultWeights();
			}
			// if can't see player anymore, begin searching
			if (!playerInSight)
				changeStateTo(SEARCH);

		}
		else if (state == SEARCH) {
			//checkNeighborFoundPlayer ();
			//Debug.DrawLine(transform.position, playerPosEstimate, Color.magenta);
			//Debug.DrawLine(playerPosEstimate, playerPosEstimate + lastKnownPlayerVelocity, Color.red);
			destination = playerPosEstimate;
			spreadOut();

		}
		else
			Debug.LogError("undefined state");

	}

	void changeStateTo(int newState) {
		if (state == newState)
			return;

		// DEBUG SEARCH STATE
		//if (newState == SEARCH)
		//	newState = WANDER;

		if (state == SEARCH && newState != SEARCH) {
			CancelInvoke("finishSearching");
			setDefaultWeights();
		}

		if (state == WANDER && newState != WANDER)
			CancelInvoke("changeDestination");

		if (newState == PURSUE)
			destination = lastKnownPlayerPos;
		else if (newState == SEARCH) {
			Invoke("finishSearching", searchTime);
		}

		state = newState;
	}

	void spreadOut() {
		setCohesionRangeShort ();
		cohesionRange *= 1.5f; // not long but not too short
		agentAvoidanceRange = cohesionRange * 0.3f;
		agentAvoidanceWeight = defaultAgentAvoidanceWeight * 1.8f;

		cohesionWeight = 0;
		alignmentWeight = 0;
		destinationWeight = Vector3.Distance(transform.position, playerPosEstimate) * 0.1f;

		// add noise to player position estimate
		//float max = sightRange * 0.66f;
		//playerPosEstimate += new Vector3 (Random.Range (-max, max), Random.Range (-max, max), Random.Range (-max, max));
	}

	void checkNeighborFoundPlayer() {
		if (state == PURSUE)
			return;
		foreach (GameObject neighbor in neighbors) {
			EnemyScript neighborSc = neighbor.GetComponent<EnemyScript>();
			if (neighborSc.state == PURSUE) {
				// set estimate of player position to the estimate of neighbor who has found player
				playerPosEstimate = neighborSc.playerPosEstimate;
				// update other stuff accordingly
				lastKnownPlayerPos = neighborSc.lastKnownPlayerPos;
				prevLastKnownPlayerPos = neighborSc.prevLastKnownPlayerPos;
				lastKnownPlayerVelocity = neighborSc.lastKnownPlayerVelocity;
				// set weights back to normal so flock not spread out in search pattern
				setDefaultWeights();
				return;
			}
		}
	}

	void setDefaultWeights() {
		obstacleAvoidanceWeight = defaultObstacleAvoidanceWeight;
		agentAvoidanceWeight = defaultAgentAvoidanceWeight;
		agentAvoidanceRange = obstacleAvoidanceRange * 0.7f;
		cohesionWeight = defaultCohesionWeight;
		alignmentWeight = defaultAlignmentWeight;
		destinationWeight = defaultDestinationWeight;
		standoffWeight = 1;
	}

	void updatePlayerPosEstimate() {
		if (playerInSight) {
			playerPosEstimate = lastKnownPlayerPos;
			//playerPosEstimate = player.transform.position;
		}
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
		if (playerInvisible) {
			playerInSight = false;
			return;
		}
		if (omniscient) {
			if (state == WANDER)
				CancelInvoke("changeDestination");
			lastKnownPlayerPos = player.transform.position;
			state = PURSUE;
			playerInSight = true;
			return;
		}
		// check to see if player in sight
		if (player && Vector3.Distance(transform.position, player.transform.position) <= sightRange) {
			Vector3 vecToPlayer = player.transform.position - transform.position;
			float angle = Mathf.Abs(Vector3.Angle(facingDirection, vecToPlayer));
			if (angle <= fov && clearLOS(gameObject, player, sightRange)) {
				updateMemoryOfPlayerPosTo(player.transform.position);
				playerInSight = true;
				return;
			}
		}
		playerInSight = false;
		
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
				avoidanceVec += vecFromObj * (1 / range);
			}
		}
		return avoidanceVec * obstacleAvoidanceWeight;
	}

	Vector3 agentCheck() {
		Vector3 avoidanceVec = Vector3.zero;
		foreach (GameObject enemy in closeNeighbors) {
			if (enemy != gameObject) {
				Vector3 vecFromObj = transform.position - enemy.transform.position;
				float range = vecFromObj.magnitude;
				vecFromObj.Normalize();
				//avoidanceVec += vecFromObj * agentAvoidanceWeight * (1 / Mathf.Pow(range, 2));
				avoidanceVec += vecFromObj * (1 / range);
			}
		}
		return avoidanceVec * agentAvoidanceWeight;
	}

	void getNeighbors() {
		neighbors.Clear ();
		closeNeighbors.Clear ();
		Collider[] cols = Physics.OverlapSphere(transform.position, cohesionRange);
		foreach (Collider col in cols) {
			if (col.gameObject.tag == "Enemy" && col.gameObject != gameObject) {
				neighbors.Add(col.gameObject);
				if (Vector3.Distance(transform.position, col.gameObject.transform.position) < agentAvoidanceRange)
					closeNeighbors.Add(col.gameObject);
			}
		}
	}

	Vector3 getCohesionVec() {
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
		return vecToLocalCent * cohesionWeight;
	}

	Vector3 getAlignmentVec () {
		// get average destination of flock
		Vector3 avgDest = destination;
		float flockSize = neighbors.Count + 1;
		foreach (GameObject obj in neighbors) {
			EnemyScript currentObj = obj.GetComponent<EnemyScript>();
			avgDest += currentObj.destination;
		}
		avgDest += destination;
		avgDest /= flockSize;
		// get vector to average destination
		Vector3 vecToAvgDest = avgDest - transform.position;
		return vecToAvgDest.normalized * alignmentWeight;
	}

	Vector3 getStandoffVec() {
		Vector3 returnVec = Vector3.zero;
		if (closeToPlayer()) {
			returnVec = transform.position - player.transform.position;
			returnVec *= standoffWeight;
		}
		return returnVec;
	}

	float smellCheck() {
		if (!player)
			return 0;
		float rangeToPlayer = Vector3.Distance (transform.position, player.transform.position);
		if (rangeToPlayer <= smellRange)
			smellLevel = 1.0f / rangeToPlayer;
		else
			smellLevel = 0;

		float deltaSmell = smellLevel - prevSmellLevel;
		deltaSmell *= smellWeight;
		//Debug.Log ("delta smell is " + deltaSmell);
		prevSmellLevel = smellLevel;

		return deltaSmell;

	}

	void getSmellMultiplier() {
		smellMultiplier = smellCheck ();
		if (smellMultiplier >= 0)
			smellMultiplier += 1;
		else
			smellMultiplier -= 1;

		smellMultiplier = Mathf.Clamp (smellMultiplier, -20.0f, 20.0f);
		//Debug.Log ("smell multiplier is " + smellMultiplier);
	}

	void dampenRigidbodyForces() {
		rigidbody.velocity = Vector3.zero;
		rigidbody.angularVelocity = Vector3.zero;
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
		f1 = Random.Range(-GameManagerScript.creationRadius, GameManagerScript.creationRadius);
		f2 = Random.Range(-GameManagerScript.creationRadius, GameManagerScript.creationRadius);
		f3 = Random.Range(-GameManagerScript.creationRadius, GameManagerScript.creationRadius);
		Vector3 newDestination = new Vector3(f1, f2, f3);
		newDestination = Vector3.ClampMagnitude (newDestination, GameManagerScript.creationRadius);
		destination = newDestination;

		//Debug.Log ("destination changed to " + newDestination);
	}

	void adjustEnergyLevel (float currentSpeed, float turnAngle) {
		float speedPenalty = (int)currentSpeed - defaultSpeed;
		if (speedPenalty > 0 && energyLevel > 0) {
			// decrement energy based on how much faster than normal (above default speed) we're going
			energyLevel -= speedPenalty * 1.5f * Time.deltaTime;
			// decrement energy based on turn angle
			energyLevel -= turnAngle * 80f * Time.deltaTime; // was 50f
		}
		else if (energyLevel < 100)
			energyLevel += energyRegenRate * Time.deltaTime;

		if (energyLevel < 0)
			energyLevel = 0;

		if (energyLevel > 100)
			energyLevel = 100;
	}

	void changeColorBasedOnState() {
		if (state == WANDER)
			renderer.material.color = Color.green;
		else if (state == PURSUE)
			renderer.material.color = Color.red;
		else if (state == SEARCH)
			renderer.material.color = Color.yellow;
		else
			renderer.material.color = Color.white; // should probably never get here

		// make color blue if energy low
		if (energyLevel <= 15) {
			//renderer.material.color *= 0.5f;
			renderer.material.color = Color.blue;
			//Debug.Log("EXHAUSTED");
		}

		trail.material.color = renderer.material.color;
	}

	bool closeToPlayer() {
		if (playerInSight
			&& Vector3.Distance (transform.position, destination) < closeInDist
			&& (state == PURSUE || state == SEARCH))
			return true;
		else
			return false;
	}

	bool checkVectorIsNaN(Vector3 test) {
		if (System.Single.IsNaN (test.x) || System.Single.IsNaN (test.y) || System.Single.IsNaN (test.z)) {
			//Debug.Log ("position is: " + rigidbody.position + ", direction is: " + direction);
			return true;
		}
		return false;
	}
	

}
