using UnityEngine;
using System.Collections;
using BehaviorTree;

public class Brain : MonoBehaviour
{
    [SerializeField]
    TextAsset m_brainFile;

    IEnumerator Start()
    {
		var blueprint = new Blueprint(m_brainFile.text);
		var rootNode = blueprint.ProduceInstance();
		var context = new Context() { ownerGameObject = gameObject };

		while (true)
		{
			rootNode.Tick(context);
			yield return null;
		}
	}
}
