using UnityEngine;
using BehaviorTree;

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
