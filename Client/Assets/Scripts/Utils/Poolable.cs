using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Poolable : MonoBehaviour
{
	public bool IsUsing;

	public void ReturnToPool(float delay = 0f)
	{
		if (delay <= 0f)
		{
			Managers.Pool.Push(this);
		}
		else
		{
			CoReturnToPool(delay).Forget();
		}
	}

	private async UniTaskVoid CoReturnToPool(float delay)
	{
		await UniTask.Delay((int)(delay * 1000));
		if (this != null && gameObject != null)
		{
			Managers.Pool.Push(this);
		}
	}
}
