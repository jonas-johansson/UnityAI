using UnityEngine;
using System.Collections;

public class PlaceOnTap : MonoBehaviour
{
	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			var hit = new RaycastHit();
			var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out hit, 300.0f))
			{
				transform.position = hit.point;
			}
		}
	}
}
