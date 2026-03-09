using System.Collections;
using UnityEngine;

public class Poolable : MonoBehaviour
{
	public bool IsUsing;

	public void ReturnToPool(float delay = 0f)
	{
		if (delay <= 0f)
			Managers.Pool.Push(this);
		else
			StartCoroutine(CoReturnToPool(delay));
	}

	private IEnumerator CoReturnToPool(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (this != null && gameObject != null)
			Managers.Pool.Push(this);
	}
}
