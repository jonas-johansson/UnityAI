using UnityEngine;
using System.Collections;
using BehaviorTree;
using BehaviorTree.Runtime;
using BehaviorTree.Serialization;
using System.Collections.Generic;

public class Brain : MonoBehaviour
{
    [SerializeField]
    TextAsset m_brainFile;

	private BehaviorTree.Runtime.Node m_treeRootNode;
	private BehaviorTree.Runtime.Context m_context;

    void Start()
    {
		var blueprint = new Blueprint(m_brainFile.text);
		m_treeRootNode = blueprint.ProduceInstance();
		m_context = new Context() { ownerGameObject = gameObject };
	}

	void Update()
	{
		m_treeRootNode.Tick(m_context);
	}

	void Test()
	{
		// Set up behavior tree
		var node = new Speak();
		node.message = "Hello";

		// Tick behavior tree
		var context = new Context() { ownerGameObject = gameObject };
		var result = node.Tick(context);



	}
}
