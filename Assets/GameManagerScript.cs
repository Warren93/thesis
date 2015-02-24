using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManagerScript : MonoBehaviour {

	public GameObject Enemy;
	GameObject Player;

	List<GameObject> asteroids;
	List<GameObject> uniqueAsteroids;
	List<GameObject> enemies;

	Rect infoBarRect;
	Rect warningRect;

	public static float creationRadius;
	public static float mapRadius;
	public static float warnRadius;

	int numUniqueObstacles = 10;
	int numObstacles = 800;
	//int numObstacles = 0;
	int numEnemies = 50;
	//int numEnemies = 0;

	float globalLowerBound = 2;
	float globalUpperBound = 55; // prev 35
	float globalBoundRatio = 0.75f;

	/*
	float boidCamDist = 0;
	float boidCamZoom = 1;
	Camera boidCam;
	*/

	// Use this for initialization
	void Start () {

		Application.targetFrameRate = 60;
		QualitySettings.antiAliasing = 4;

		infoBarRect = new Rect (10, 10, Screen.width * 0.2f, 20);
		warningRect = new Rect (0, 0, Screen.width * 0.2f, 45);
		warningRect.center = new Vector2 (Screen.width * 0.5f, Screen.height * 0.5f);

		creationRadius = 800.0f;
		mapRadius = creationRadius * 0.9f;
		warnRadius = mapRadius - 120;


		/*
		boidCam = (Camera) Instantiate(GameObject.FindGameObjectWithTag("MainCamera").camera,
		                               Vector3.zero,
		                               Quaternion.FromToRotation(new Vector3(0, 0, 0), new Vector3(0, 0, 1)));

		boidCam.enabled = false;
		*/

		asteroids = new List<GameObject> ();
		uniqueAsteroids = new List<GameObject> ();
		enemies = new List<GameObject>();
		Player = GameObject.FindGameObjectWithTag ("Player");

		// create enemy
		for (int i = 0; i < numEnemies; i++) {
			//Vector3 spawnPos = Vector3.zero + Vector3.back * 70;
			Vector3 spawnPos = Vector3.back * 200;
			float f1, f2, f3;
			/*
			f1 = Random.Range(-10, 10);
			f2 = Random.Range(-10, 10);
			f3 = Random.Range(-10, 10);
			Vector3 createPt = new Vector3(f1, f2, f3);
			createPt.Normalize();
			createPt *= Random.Range(60, 120);
			*/
			f1 = Random.Range(-120, 120);
			f2 = Random.Range(-120, 120);
			f3 = Random.Range(-120, 120);
			Vector3 createPt = new Vector3(f1, f2, f3);
			spawnPos += createPt;
			spawnEnemyAt (spawnPos);
		}

		Screen.showCursor = false;

		createAsteroids ();

	}
	
	// Update is called once per frame
	void Update () {

		// damage player if they get too far from the center of the game world
		if (Player && Vector3.Distance(Vector3.zero, Player.transform.position) > mapRadius) {
			PlayerScript playerInfo = Player.GetComponent<PlayerScript> ();
			playerInfo.hitpoints -= 60 * Time.deltaTime;
		}

		if (Input.GetKeyUp(KeyCode.V)) {
			foreach (GameObject enemy in enemies) {
				EnemyScript currentEnemy = enemy.GetComponent<EnemyScript>();
				if (currentEnemy.enabled == false)
					currentEnemy.enabled = true;
			}
		}

		/*
		Vector3 centroid = Vector3.zero;
		Vector3 flockDirection = Vector3.zero;
		foreach (GameObject enemy in enemies) {
			centroid += enemy.transform.position;
			flockDirection += enemy.GetComponent<EnemyScript>().direction;
		}
		centroid /= enemies.Count;
		flockDirection.Normalize ();

		foreach (GameObject enemy in enemies) {
			float dist = Vector3.Distance(enemy.transform.position, centroid);
			if (dist > boidCamDist)
				boidCamDist = dist;
		}

		boidCam.transform.position = centroid - (flockDirection * boidCamDist * boidCamZoom);
		boidCam.transform.LookAt (centroid);

		if (Input.GetKey (KeyCode.B)) {
			foreach (Camera cam in Camera.allCameras) {
				cam.enabled = false;
				if (cam.GetComponent<AudioListener>() != null)
					cam.GetComponent<AudioListener>().enabled = false;
			}
			boidCam.enabled = true;
		}


		if (Input.GetKey (KeyCode.UpArrow))
						boidCamZoom -= 0.1f;
		if (Input.GetKey (KeyCode.DownArrow))
						boidCamZoom += 0.1f;
						
		*/

		// toggle asteroids on/off with M key
		if (Input.GetKeyDown (KeyCode.M)) {
			foreach (GameObject obj in asteroids) {
				if (obj.activeSelf == true)
					obj.SetActive(false);
				else if (obj.activeSelf == false)
					obj.SetActive(true);
			}
		}

		/*
		for (int i = 0; i < enemies.Count; i++) {
			Debug.DrawLine(Player.transform.position, enemies[i].transform.position, Color.red);
		}
		*/

		/*
		foreach (GameObject enemy in enemies) {
			EnemyScript currentEnemy = enemy.GetComponent<EnemyScript>();
			if (currentEnemy.state != EnemyScript.WANDER) {
				Debug.DrawLine(enemy.transform.position, currentEnemy.playerPosEstimate, Color.green);
				Debug.DrawRay(enemy.transform.position, currentEnemy.alignmentVec, Color.red);
			}
		}
		*/

	}

	void OnGUI() {
		if (Player) {
			PlayerScript playerInfo = Player.GetComponent<PlayerScript> ();
			GUI.Box(infoBarRect,
			        "Boost: " + (int)playerInfo.boostCharge + "   Health: " + (int)playerInfo.hitpoints);
			float playerDistFromOrigin = Vector3.Distance(Vector3.zero, Player.transform.position);
			float distToEdge = mapRadius - playerDistFromOrigin;
			if (playerDistFromOrigin > warnRadius) {
				GUI.Box(warningRect, "Approaching edge of game area (distance: " + (int)distToEdge
				        + ")\nTurn back or you will take damage");
			}
		}
	}

	void spawnEnemyAt(Vector3 position) {
		GameObject enemy = (GameObject) Instantiate (Enemy);
		enemy.transform.position = position;
		enemies.Add (enemy);
		enemy.GetComponent<EnemyScript> ().enabled = false;
		enemy.renderer.material.color = Color.green;
		enemy.GetComponent<EnemyScript> ().gameManger = gameObject;
	}

	void createAsteroids() {
		for (int i = 0; i < numUniqueObstacles; i++) {
			GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

			float upperBound = Random.Range(globalLowerBound, globalUpperBound);
			float lowerBound = upperBound * globalBoundRatio;
			
			// randomize shape
			Mesh sphereMesh = sphere.GetComponent<MeshFilter>().mesh;
			Vector3[] verts = sphereMesh.vertices;
			Vector3 avgVertPos = Vector3.zero;
			for (int j = 0; j < verts.Length; j++)
				avgVertPos += verts[j];
			avgVertPos /= verts.Length;
			float originalRadius = Vector3.Distance(verts[0], avgVertPos);
			
			//for (int j = 0; j < verts.Length; j++) {
			//	Debug.Log("Vert " + j + ": " + verts[j]);
			//}
			
			//Debug.Log("Max triangle vert index is " + maxIdx);
			Hashtable uniqueVerts = new Hashtable();
			
			for (int j = 0; j < verts.Length; j++) {
				// if we've already encountered this vertex...
				if (uniqueVerts.Contains(verts[j].ToString())) {
					// ...retrieve the value we gave it last time
					//string oldVertString = verts[j].ToString();
					verts[j] = (Vector3) uniqueVerts[verts[j].ToString()];
					//Debug.Log("already encountered vertex " + oldVertString + ", using prev. value of " + verts[j]);
				}
				else { // otherwise...
					// ...generate new vertex value...
					Vector3 vecFromCenter = verts[j] - avgVertPos;
					float originalVertRadius = vecFromCenter.magnitude;
					vecFromCenter.Normalize();
					float rf = Random.Range(lowerBound * originalVertRadius, upperBound * originalVertRadius);
					Vector3 newVertexValue = verts[j] + vecFromCenter * rf;
					// ...and store it in this vertex's position in hashtable
					//Debug.Log("first time encountering vertex " + verts[j] + ", storing new value " + newVertexValue);
					uniqueVerts.Add(verts[j].ToString(), newVertexValue);
					verts[j] = newVertexValue;
				}
			}
			
			float newRadius = 0;
			for (int j = 0; j < verts.Length; j++) {
				float currentVertDist = Vector3.Distance(verts[j], avgVertPos);
				if (Vector3.Distance(verts[j], avgVertPos) > newRadius)
					newRadius = currentVertDist;
			}
			float scaleFactor = newRadius / originalRadius;
			
			SphereCollider col = sphere.transform.GetComponent<SphereCollider>();
			col.radius *= scaleFactor;
			
			sphereMesh.vertices = verts;
			sphereMesh.RecalculateNormals();
			sphereMesh.RecalculateBounds();
			uniqueVerts.Clear();

			uniqueAsteroids.Add(sphere);
		}

		for (int i = 0; i < numObstacles; i++) {
			float f1, f2, f3;
			/*
			f1 = Random.Range(-10, 10);
			f2 = Random.Range(-10, 10);
			f3 = Random.Range(-10, 10);
			Vector3 createPt = new Vector3(f1, f2, f3);
			createPt.Normalize();
			createPt *= Random.Range(20, creationRadius);
			*/
			f1 = Random.Range(-creationRadius, creationRadius);
			f2 = Random.Range(-creationRadius, creationRadius);
			f3 = Random.Range(-creationRadius, creationRadius);
			Vector3 createPt = new Vector3(f1, f2, f3);
			int idx = Random.Range(0, numUniqueObstacles - 1);
			GameObject obstacle = (GameObject) Instantiate(uniqueAsteroids[idx]);
			obstacle.transform.position = createPt;
			obstacle.isStatic = true;
			obstacle.tag = "Obstacle";

			asteroids.Add(obstacle);

		}

		for (int i = 0; i < uniqueAsteroids.Count; i++) {
			Destroy(uniqueAsteroids[i]);
		}

	}
}
