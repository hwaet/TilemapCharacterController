using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class TilemapCharacterController : MonoBehaviour
{
	[HideInInspector]
	public Vector3 currentVelocity;
	private Transform lastPosition;

	[Tooltip("Distance padding added to each collision raycast, to help prevent getting stuck in geometry. Small values are ideal. ")]
	public float skinThickness;
	List<Collider2D> characterColliders;
	private float ANGLE_TO_SLIDE_ALONG_SURFACES=30f;
	[HideInInspector]
	public bool recaculateSlidingDirection = false;

	[Tooltip("collision layers that the character will be blocked by.")]
	public LayerMask collisionLayers;
	[Tooltip("The collision layer of the character itself. Used for efficiency and to prevent odd self-collision calculations.")]
	public LayerMask characterCollisionLayer;

	void Awake()
	{
		characterColliders = GetComponentsInChildren<Collider2D>().ToList();
	}


	/// <summary>
	/// Attempts to move the character in the intended direction, and will slide along colliders. Character collider is based on the aggregate of all colliders in this gameobject's hierarchy. Returns collision flags based on what's currently colliding with the character.
	/// </summary>
	/// <param name="intendedDirection">Delta movement.</param>
	public CollisionFlags Move(Vector2 intendedDirection = new Vector2())
	{
		if ((intendedDirection.magnitude == 0) && (Time.deltaTime > 0)) { return CollisionFlags.None; }
		lastPosition = this.transform;

		// modify incoming direction vector based on any encountered colliders.
		CollisionFlags flags = movementCheck(ref intendedDirection);

		// pop the character out of any objects it may become stuck in.
		forceOutOfCollider(intendedDirection);

		// move with modified vector
		transform.Translate(intendedDirection, Space.World);

		// set velocity
		currentVelocity = (transform.position - lastPosition.position);

		return flags;
	}


	/// <summary>
	/// Moves character according to the input speed. Speed is units per second. Returns whether the character is grounded, which is always true for this controller
	/// </summary>
	/// <param name="movementSpeed"></param>
	public bool SimpleMove(Vector2 movementSpeed = new Vector2())
	{
		Move(movementSpeed * Time.deltaTime);
		return true; //the default unity character controller returns true if the character is grounded
	}


	/// <summary>
	/// For every point in the character's colliders, check for possible collisions and modify the intended direction vector accordingly
	/// </summary>
	/// <param name="intendedDirection"></param>
	/// <returns></returns>
	private CollisionFlags movementCheck(ref Vector2 intendedDirection)
	{
		Vector2[] rayCastPoints = getRaycastPoints();
		CollisionFlags flags = CollisionFlags.None;

		foreach (Vector2 point in rayCastPoints)
		{
			CollisionFlags newflags = rayCastFromPoint(point, ref intendedDirection);
			flags = flags | newflags;
		}

		return flags;
	}


	/// <summary>
	/// Cast a raycast along the intended vector of motion, and determine if we'll hit a collider or not.
	/// </summary>
	/// <param name="worldPoint"></param>
	/// <param name="intendedDirection"></param>
	/// <returns></returns>
	private CollisionFlags rayCastFromPoint(Vector2 worldPoint, ref Vector2 intendedDirection)
	{
		Vector2 intendedX = new Vector2(intendedDirection.x, 0);
		Vector2 intendedY = new Vector2(0, intendedDirection.y);
		CollisionFlags flags = CollisionFlags.None;

		float xDist = measureDistanceToCollider(worldPoint, intendedX);
		float yDist = measureDistanceToCollider(worldPoint, intendedY);
		float xSign = (intendedDirection.x < 0 ? -1 : 1);
		float ySign = (intendedDirection.y < 0 ? -1 : 1);

		if (Application.isEditor == true)  Debug.DrawRay(worldPoint, intendedDirection * 10, Color.white);

		if (xDist >= 0)
		{
			if (Mathf.Abs(xDist) < Mathf.Abs(intendedDirection.x)) intendedDirection.x = xDist * xSign;
			flags = flags | CollisionFlags.Sides;
		}
		if (yDist >= 0)
		{
			if (Mathf.Abs(yDist) < Mathf.Abs(intendedDirection.y)) intendedDirection.y = yDist * ySign;
			if (ySign > 0) { flags = flags | CollisionFlags.Above; }
			if (ySign < 0) { flags = flags | CollisionFlags.Below; }
		}

		return flags;
	}


	/// <summary>
	/// Returns a list of points to raycast from, based on all the child component colliders.
	/// </summary>
	/// <returns></returns>
	private Vector2[] getRaycastPoints()
	{
		Vector2[] points = new Vector2[0];

		//if it's a poly collider
		for (int i = 0; i < characterColliders.Count; i++)
		{
			Collider2D collider = characterColliders[i];

			if (collider.GetType() == typeof(PolygonCollider2D))
			{
				points = getPolygonColliderVerts(collider);
			}

			//TODO properly concantenate the points list

			if (collider.GetType() == typeof(CircleCollider2D))
			{
				points = getCircleColliderVerts(collider);
			}

			if (collider.GetType() == typeof(BoxCollider2D))
			{
				points = getBoxColliderVerts(collider);
			}

			if (collider.GetType() == typeof(CompositeCollider2D))
			{
				points = getCompositeColliderVerts(collider);
			}
		}

		return points;
	}


	private Vector2[] getCompositeColliderVerts(Collider2D collider)
	{
		throw new NotImplementedException();
	}


	private Vector2[] getBoxColliderVerts(Collider2D collider)
	{
		throw new NotImplementedException();
	}


	private Vector2[] getCircleColliderVerts(Collider2D collider)
	{
		throw new NotImplementedException();
	}


	private static Vector2[] getPolygonColliderVerts(Collider2D collider)
	{
		Vector2[] points;
		PolygonCollider2D polyCollider = (PolygonCollider2D)collider;

		points = polyCollider.points;

		//transform points into proper world space position
		for (int newIndex = 0; newIndex < points.Length; newIndex++)
		{
			points[newIndex] = polyCollider.transform.TransformPoint(points[newIndex]);
		}

		return points;
	}


	/// <summary>
	/// 
	/// </summary>
	/// <param name="worldPoint"></param>
	/// <param name="intendedDirection"></param>
	/// <returns></returns>
	private float measureDistanceToCollider(Vector2 worldPoint, Vector2 intendedDirection)
	{
		Vector3 skinPoint = (intendedDirection.normalized * skinThickness) + worldPoint;
		int combinedMasks = (collisionLayers | characterCollisionLayer);
		RaycastHit2D raycastHitTemp = Physics2D.Raycast(skinPoint, intendedDirection, intendedDirection.magnitude, combinedMasks);

		if (hitNothing(raycastHitTemp) == true) { return -1; }
		if (hitSelf(raycastHitTemp) == true) { return -1; }

		float newDistance = raycastHitTemp.distance;
		//draw connectionPiont
		if (Application.isEditor==true)
		{
			Debug.DrawLine(worldPoint, skinPoint, Color.black);
			Debug.DrawRay(raycastHitTemp.point, Vector2.up * .1f, Color.yellow);
			Debug.DrawRay(raycastHitTemp.point, Vector2.down * .1f, Color.yellow);
			Debug.DrawRay(raycastHitTemp.point, Vector2.left * .1f, Color.yellow);
			Debug.DrawRay(raycastHitTemp.point, Vector2.right * .1f, Color.yellow);
		}

		if (recaculateSlidingDirection == true)
		{
			newDistance = getSlideDistance(worldPoint, intendedDirection);
		}

		return newDistance;
	}

	/// <summary>
	/// Did the raycast hit nothing? Returns true if the raycast encountered no colliders
	/// </summary>
	/// <param name="raycastHitTemp"></param>
	/// <returns></returns>
	private bool hitNothing(RaycastHit2D raycastHitTemp)
	{
		return (raycastHitTemp.collider == null);
	}

	/// <summary>
	/// Did the raycast hit the character's own colliders? Returns true if the raycast hits the character.
	/// </summary>
	/// <param name="raycastHitTemp"></param>
	/// <returns></returns>
	private bool hitSelf(RaycastHit2D raycastHitTemp)
	{
		if (raycastHitTemp.collider == null) { return false; }
		int colliderLayer = 1 << raycastHitTemp.collider.gameObject.layer;
		if ((colliderLayer & characterCollisionLayer) != 0) { return true; }
		else { return false; }
	}


	/// <summary>
	/// Recalculate two vectors that are rotated x degrees off-axis from the given vector, where x is set by a local constant. This is useful for implementing a direction vector to slide along a surface
	/// </summary>
	/// <param name="worldPoint"></param>
	/// <param name="intendedDirection"></param>
	/// <returns></returns>
	private float getSlideDistance(Vector2 worldPoint, Vector2 intendedDirection)
	{
		LayerMask outsideColliders = collisionLayers;

		Vector2 upDir = intendedDirection.Rotate(ANGLE_TO_SLIDE_ALONG_SURFACES);
		Vector2 upSkinPoint = (upDir.normalized * skinThickness) + worldPoint;
		if (Application.isEditor == true)  Debug.DrawRay(upSkinPoint, upDir * 10, Color.blue);
		RaycastHit2D raycastHitUpTemp = Physics2D.Raycast(upSkinPoint, upDir, upDir.magnitude, outsideColliders);

		Vector2 downDir = intendedDirection.Rotate(ANGLE_TO_SLIDE_ALONG_SURFACES*-1);
		Vector2 downSkinPoint = (downDir.normalized * skinThickness) + worldPoint;
		if (Application.isEditor == true)  Debug.DrawRay(downSkinPoint, downDir * 10, Color.blue);
		RaycastHit2D raycastHitDownTemp = Physics2D.Raycast(downSkinPoint, downDir, downDir.magnitude, outsideColliders);

		float[] values = new float[2];
		if (raycastHitUpTemp.collider == null) { values[0] = intendedDirection.magnitude; }
		else { values[0] = raycastHitUpTemp.distance; }
		if (raycastHitDownTemp.collider == null) { values[1] = intendedDirection.magnitude; }
		else { values[1] = raycastHitDownTemp.distance; }

		return Mathf.Max(values);
	}


	/// <summary>
	/// Recursively disable all colliders under this gameobjects
	/// </summary>
	public void disableCollisions()
	{
		foreach (Collider2D col in characterColliders)
		{
			col.enabled = false;
		}
	}


	/// <summary>
	/// Recursively enable all colliders under this gameobjects
	/// </summary>
	public void enableCollisions()
	{
		foreach (Collider2D col in characterColliders)
		{
			col.enabled = true;
		}
	}


	/// <summary>
	/// Moves the character along the given vector until it no longer interpenetrates an external collider. 
	/// </summary>
	/// <param name="intendedDirection"></param>
	private void forceOutOfCollider(Vector2 intendedDirection)
	{
		Vector2[] rayCastPoints = getRaycastPoints();
		
		foreach (Vector2 point in rayCastPoints)
		{
			LayerMask outsideColliders = collisionLayers;
			Collider2D col = Physics2D.OverlapPoint(point, outsideColliders);
			Vector2 newpoint = point + intendedDirection;
			while (col != null)
			{
				Vector2 outDir = (new Vector2(this.transform.position.x, this.transform.position.y) - point);
				while (newpoint == col.ClosestPoint(newpoint))
				{
					newpoint = newpoint + (outDir *.01f);
				}
				transform.Translate((newpoint - point), Space.World);
				col = Physics2D.OverlapPoint(newpoint, outsideColliders);
			}
		}
	}
}


public static class Vector2Extension
{
	public static Vector2 Rotate(this Vector2 sourceVector, float degrees)
	{
		float radians = degrees * Mathf.Deg2Rad;
		float sin = Mathf.Sin(radians);
		float cos = Mathf.Cos(radians);

		return new Vector2(cos * sourceVector.x - sin * sourceVector.y, sin * sourceVector.x + cos * sourceVector.y);
	}
}
