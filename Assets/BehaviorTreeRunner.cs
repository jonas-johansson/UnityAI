using UnityEngine;
using BehaviorTree;

/// <summary>
/// Add this component to a game object that you want to run a behavior tree on.
/// </summary>
public class BehaviorTreeRunner : MonoBehaviour
{
    [SerializeField]
    private TextAsset m_brainFile;
	private BehaviorTreeInstance m_treeInstance;

    void Start()
    {
		m_treeInstance = new BehaviorTreeInstance(owner: gameObject, script: m_brainFile.text);
	}

	void Update()
	{
		m_treeInstance.Update();
	}
}
