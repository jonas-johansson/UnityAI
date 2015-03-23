using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

namespace BehaviorTree
{
	public class Parameter
	{
		public string key;
		public string value;

		public Parameter(string str)
		{
			var parts = str.Split('=');
			key = parts[0];
			value = parts[1];
		}
	}

	public class NodeDesc
	{
		public string typeName;
		public List<Parameter> parameters = new List<Parameter>();
		public List<NodeDesc> children = new List<NodeDesc>();
	}

	public class Blueprint
	{
		public NodeDesc root;

		public Blueprint(string text)
		{
			var lines = text.Split('\n');

			Stack<NodeDesc> stack = new Stack<NodeDesc>();
			NodeDesc prev = null;

			foreach (var unfilteredLine in lines)
			{
				var line = unfilteredLine
					.Replace("\t", string.Empty)
					.Replace("\r", string.Empty);

				var node = new NodeDesc();

				var tokens = line.Split(' ');
				node.typeName = tokens[0];

				int depth = TreeDepth(unfilteredLine);
				if (depth > stack.Count)
					stack.Push(prev);
				else if (depth < stack.Count)
					stack.Pop();

				if (stack.Count == 0)
				{
					this.root = node;
				}
				else
				{
					var parent = stack.Peek();
					parent.children.Add(node);
				}

				// Read parameters
				for (int i = 1; i < tokens.Length; ++i)
				{
					node.parameters.Add(new Parameter(tokens[i]));
				}

				prev = node;
			}
		}

		private int TreeDepth(string line)
		{
			int level = 0;

			for (int i = 0; i < line.Length; ++i)
			{
				if (char.IsWhiteSpace(line[i]))
					level++;
				else
					break;
			}
				
			return level;
		}

		public Node ProduceInstance()
		{
			Node node = NodeFactory.Create(this.root.typeName);
			node.Deserialize(this.root);
			return node;
		}
	}

	public class NodeFactory
	{
		public static Node Create(string typeName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (var type in GetNodeTypes(assembly))
				{
					if (type.Name.ToLower() == typeName.ToLower())
					{
						var node = Activator.CreateInstance(type);
						return (Node)node;
					}
				}
			}

			throw new Exception(string.Format("{0} is an unknown node type", typeName));
		}

		static IEnumerable<Type> GetNodeTypes(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypes())
			{
				if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Node)))
				{
					yield return type;
				}
			}
		}
	}

	public enum Status
	{
		Failure,
		Success,
		Running
	}

	public class Memory
	{
		private Dictionary<string, object> dict = new Dictionary<string, object>();

		public void Remember(string key, object obj)
		{
			dict[key] = obj;
		}

		public bool Recall<T>(string key, out T obj)
		{
			object temp = null;
			if (dict.TryGetValue(key, out temp))
			{
				obj = (T)temp;
				return true;
			}
			else
			{
				obj = default(T);
				return false;
			}
		}
	}

	public class Context
	{
		public GameObject ownerGameObject;
		public Memory memory = new Memory();
	}

	public abstract class Node
	{
		private bool started = false;

		protected List<Node> children = new List<Node>();

		public Status Tick(Context context)
		{
			if (!started)
			{
				OnStart(context);
				started = true;
			}

			Status s = OnUpdate(context);

			if (s != Status.Running)
			{
				Stop();
			}

			return s;
		}

		protected virtual void OnStart(Context context)
		{
		}

		protected virtual Status OnUpdate(Context context)
		{
			return Status.Success;
		}

		protected virtual void OnStop()
		{
		}

		public void Stop()
		{
			if (this.started)
			{
				OnStop();
				this.started = false;
			}
		}

		public void Deserialize(NodeDesc node)
		{
			PopulateFields(node);

			for (int i = 0; i < node.children.Count; ++i)
			{
				var child = NodeFactory.Create(node.children[i].typeName);
				children.Add(child);
				child.Deserialize(node.children[i]);
			}
		}

		private void PopulateFields(NodeDesc node)
		{
			var fields = this.GetType().GetFields();
			foreach (var field in fields)
			{
				foreach (var parameter in node.parameters)
				{
					if (parameter.key.ToLower() == field.Name.ToLower())
					{
						if (field.FieldType == typeof(int))
							field.SetValue(this, int.Parse(parameter.value));
						else if (field.FieldType == typeof(float))
							field.SetValue(this, float.Parse(parameter.value));
						else if (field.FieldType == typeof(string))
							field.SetValue(this, parameter.value);
					}
				}
			}
		}
	}

	public class Sequence : Node
	{
		private List<Node>.Enumerator currentChild;
		private bool childIsValid = false;

		protected override void OnStart(Context context)
		{
			// Move to the first child
			currentChild = children.GetEnumerator();
			currentChild.MoveNext();
			childIsValid = true;
		}

		protected override Status OnUpdate(Context context)
		{
			while (true)
			{
				Status s = currentChild.Current.Tick(context);

				switch (s)
				{
					case Status.Running:
					case Status.Failure:
						return s;

					case Status.Success:
						if (!currentChild.MoveNext())
						{
							childIsValid = false;
							return Status.Success;
						}
						break;
				}
			}
		}

		protected override void OnStop()
		{
			if (childIsValid)
			{
                currentChild.Current.Stop();
				childIsValid = false;
			}
		}
	}

	public class Selector : Node
	{
		private List<Node>.Enumerator currentChild;

		protected override void OnStart(Context context)
		{
			// Move to the first child
			currentChild = this.children.GetEnumerator();
			currentChild.MoveNext();
		}

		protected override Status OnUpdate(Context context)
		{
			while (true)
			{
				Status s = currentChild.Current.Tick(context);

				switch (s)
				{
					case Status.Running:
					case Status.Success:
						return s;

					case Status.Failure:
						if (!currentChild.MoveNext())
							return Status.Failure;
						break;
				}
			}
		}

		protected override void OnStop()
		{
			if (currentChild.Current != null)
			{
				currentChild.Current.Stop();
			}
		}
	}

	public class Log : Node
	{
		public string message;

		protected override void OnStart(Context context)
		{
			Debug.Log("BehaviorTree: " + message + "\n");
		}

		protected override Status OnUpdate(Context context)
		{
			return Status.Success;
		}

		protected override void OnStop()
		{
		}
	}

	public class Speak : Node
	{
		public string message;

		protected override void OnStart(Context context)
		{
			var textMesh = context.ownerGameObject.GetComponentInChildren<TextMesh>();
			if (textMesh)
			{
				textMesh.text = message;
			}
		}
	}

	public class FindGameObject : Node
	{
		public string name;
		public string tag;
		public string rememberAs;

		protected override Status OnUpdate(Context context)
		{
			GameObject obj = Find();

			if (obj != null)
			{
				context.memory.Remember(rememberAs, obj);
				return Status.Success;
			}
			else
			{
				return Status.Failure;
			}
		}

		private GameObject Find()
		{
			if (!string.IsNullOrEmpty(name))
				return GameObject.Find(name);
			else if (!string.IsNullOrEmpty(tag))
				return GameObject.FindGameObjectWithTag(tag);
			else
				return null;
		}
	}

	public class FindRandomGameObject : Node
	{
		public string tag;
		public string rememberAs;
		public float minDistance = -1.0f;
		public float maxDistance = float.PositiveInfinity;

		protected override Status OnUpdate(Context context)
		{
			GameObject obj = Find(context.ownerGameObject.transform.position);

			if (obj != null)
			{
				context.memory.Remember(rememberAs, obj);
				return Status.Success;
			}
			else
			{
				return Status.Failure;
			}
		}

		private GameObject Find(Vector3 agentPos)
		{
			var objects = GameObject.FindGameObjectsWithTag(tag).ToList()
				.Where(obj => {
					float dist = Vector3.Distance(obj.transform.position, agentPos);
					return dist > minDistance && dist < maxDistance;
				}).ToList();

            if (objects.Count > 0)
				return objects[UnityEngine.Random.Range(0, objects.Count)];
			else
				return null;
		}
	}

	public class FindClosestUnlockedGameObject : Node
	{
		public string tag;
		public string rememberAs;
		public string around = "";
		public float minDistance = -1.0f;
		public float maxDistance = float.PositiveInfinity;

		protected override Status OnUpdate(Context context)
		{
			GameObject obj = Find(context);

			if (obj != null)
			{
				context.memory.Remember(rememberAs, obj);
				return Status.Success;
			}
			else
			{
				return Status.Failure;
			}
		}

		private GameObject Find(Context context)
		{
			GameObject aroundObj;
			if (!context.memory.Recall(around, out aroundObj))
			{
				aroundObj = context.ownerGameObject;
			}

			Vector3 aroundPos = aroundObj.transform.position;

			var objects = GameObject.FindGameObjectsWithTag(tag).ToList()
				.Where(obj => {
					float dist = Vector3.Distance(obj.transform.position, aroundPos);
					bool withinDistance = dist > minDistance && dist < maxDistance;

					var lockable = obj.GetComponent<Lockable>();
					bool locked = lockable != null ? lockable.locked : false;

					return withinDistance && !locked;
				})
				.OrderBy(obj => Vector3.Distance(obj.transform.position, aroundPos))
				.ToList();

			if (objects.Count > 0)
				return objects[0];
			else
				return null;
		}
	}

	public class LockGameObject : Node
	{
		public string subject;

		private Lockable lockable;
		private bool hasLock = false;

		protected override void OnStart(Context context)
		{
			GameObject subjectObj;
            if (context.memory.Recall(subject, out subjectObj))
			{
				lockable = subjectObj.GetComponent<Lockable>();
				if (lockable != null && !lockable.locked)
				{
					lockable.locked = true;
					hasLock = true;
					return;
				}
			}

			hasLock = false;
		}

		protected override Status OnUpdate(Context context)
		{
			if (hasLock)
			{
				return children[0].Tick(context);
			}
			else
			{
				return Status.Failure;
			}
		}

		protected override void OnStop()
		{
			if (hasLock && lockable != null)
			{
				lockable.locked = false;
				lockable = null;
			}
		}
	}

	public class Wait : Node
	{
		public float duration;
		private float startTime;

		protected override void OnStart(Context context)
		{
			this.startTime = Time.time;
		}

		protected override Status OnUpdate(Context context)
		{
			return ElapsedTime > duration ? Status.Success : Status.Running;
		}

		private float ElapsedTime { get { return Time.time - startTime; } }
	}

	public class Move : Node
	{
		public string to;
		public float stopDistance;

		private GameObject toGameObject;
		private NavMeshAgent navMeshAgent;
		private NavMeshPath path = new NavMeshPath();
		private Vector3 targetPosAtLastPathing;
        private Status pendingStatus = Status.Running;
		private Animator animator;
		private AgentAnimationProperties animProperties;

		protected override void OnStart(Context context)
		{
			context.memory.Recall(to, out toGameObject);
			navMeshAgent = context.ownerGameObject.GetComponentInChildren<NavMeshAgent>();
			navMeshAgent.stoppingDistance = stopDistance;
            pendingStatus = RecalculatePath();
			animator = context.ownerGameObject.GetComponentInChildren<Animator>();
			if (animator && pendingStatus == Status.Running)
			{
				animator.SetBool("Moving", true);
			}
			animProperties = context.ownerGameObject.GetComponentInChildren<AgentAnimationProperties>();
        }

		protected override Status OnUpdate(Context context)
		{
			if (Vector3.Distance(targetPosAtLastPathing, toGameObject.transform.position) > 0.5)
			{
				pendingStatus = RecalculatePath();
			}

			if (navMeshAgent.hasPath && navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete)
			{
				bool arrived = (navMeshAgent.remainingDistance - navMeshAgent.stoppingDistance) <= 0.5f;
				if (arrived)
				{
                    pendingStatus = Status.Success;
				}
			}

			if (animator)
			{
				float multiplier = animProperties ? animProperties.moveAnimMultiplier : 0.15f;
                animator.speed = navMeshAgent.velocity.magnitude * multiplier;
			}

			return pendingStatus;
		}

		protected override void OnStop()
		{
			if (animator)
			{
				animator.SetBool("Moving", false);
				animator.speed = 1.0f;
			}
			navMeshAgent.Stop();
		}

		private Status RecalculatePath()
		{
			if (toGameObject)
			{
				Vector3 target = toGameObject.transform.position;
				targetPosAtLastPathing = target;
				path = new NavMeshPath();
				navMeshAgent.CalculatePath(target, path);

				if (path.status == NavMeshPathStatus.PathComplete)
				{
					// If the actual end position is too far away from the desired
					// end position we consider our movement a failure.
					Vector3 pathEnd = path.corners[path.corners.Length - 1];
                    if (GroundDistance(target, pathEnd) < 0.1f)
                    {
                        if (navMeshAgent.SetPath(path) && navMeshAgent.hasPath)
                            return Status.Running;
                        else
                            return Status.Success;
                    }
				}
			}

			// Failure
			path = new NavMeshPath();
            return Status.Failure;
		}

		private float GroundDistance(Vector3 a, Vector3 b)
		{
			a.y = b.y = 0;
			return Vector3.Distance(a, b);
		}

		private bool AtDestination
		{
			get
			{
				return navMeshAgent.hasPath && navMeshAgent.remainingDistance <= 0.5f;
			}
		}
	}

	public class Random : Node
	{
		private Node selectedChild;

		protected override void OnStart(Context context)
		{
			selectedChild = children[UnityEngine.Random.Range(0, children.Count)];
		}

		protected override Status OnUpdate(Context context)
		{
			return selectedChild.Tick(context);
		}

		protected override void OnStop()
		{
			if (selectedChild != null)
			{
				selectedChild.Stop();
				selectedChild = null;
			}
		}
	}

    public class Break : Node
    {
        protected override Status OnUpdate(Context context)
        {
            Debug.Break();
            return Status.Success;
        }
    }
}