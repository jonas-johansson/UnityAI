using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace BehaviorTree
{
	namespace Serialization
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

		public class Node
		{
			public string typeName;
			public List<Parameter> parameters = new List<Parameter>();
			public List<Node> children = new List<Node>();
		}

		public class Blueprint
		{
			public Node root;

			public Blueprint(string text)
			{
				var lines = text.Split('\n');

				Stack<Node> stack = new Stack<Node>();
				Node prev = null;

				foreach (var unfilteredLine in lines)
				{
					var line = unfilteredLine
						.Replace("\t", string.Empty)
						.Replace("\r", string.Empty);

					var node = new Node();

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

			public Runtime.Node ProduceInstance()
			{
				Runtime.Node node = RuntimeNodeFactory.Produce(this.root.typeName);
				node.Deserialize(this.root);
				return node;
			}
		}

		public class RuntimeNodeFactory
		{
			public static Runtime.Node Produce(string typeName)
			{
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					foreach (var type in GetNodeTypes(assembly))
					{
						if (type.Name.ToLower() == typeName.ToLower())
						{
							var node = Activator.CreateInstance(type);
							return (Runtime.Node)node;
						}
					}
				}

				throw new Exception(string.Format("{0} is an unknown node type", typeName));
			}

			static IEnumerable<Type> GetNodeTypes(Assembly assembly)
			{
				foreach (Type type in assembly.GetTypes())
				{
					//if (type.GetCustomAttributes(typeof(BehaviorTree.Runtime.BehaviorNodeAttribute), true).Length > 0)
					if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Runtime.Node)))
					{
						yield return type;
					}
				}
			}
		}
	}

	namespace Runtime
	{
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

			public void Deserialize(Serialization.Node node)
			{
				PopulateFields(node);

				for (int i = 0; i < node.children.Count; ++i)
				{
					var child = Serialization.RuntimeNodeFactory.Produce(node.children[i].typeName);
					children.Add(child);
					child.Deserialize(node.children[i]);
				}
			}

			private void PopulateFields(Serialization.Node node)
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
						case Status.Failure:
							return s;

						case Status.Success:
							if (!currentChild.MoveNext())
								return Status.Success;
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
			
			private GameObject toGameObject;
			private NavMeshAgent navMeshAgent;
			private NavMeshPath path = new NavMeshPath();
			private Vector3 targetPosAtLastPathing;

			protected override void OnStart(Context context)
			{
				context.memory.Recall(to, out toGameObject);
				navMeshAgent = context.ownerGameObject.GetComponentInChildren<NavMeshAgent>();
				RecalculatePath();
			}

			protected override Status OnUpdate(Context context)
			{
				if (Vector3.Distance(targetPosAtLastPathing, toGameObject.transform.position) > 0.5)
				{
					RecalculatePath();
				}

				if (path.status != NavMeshPathStatus.PathComplete)
				{
					Debug.Log("Couldn't reach");
					return Status.Failure;
				}

				if (navMeshAgent.hasPath && navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete)
				{
					bool arrived = navMeshAgent.remainingDistance <= 0.5f;
					if (arrived)
					{
						Debug.Log("Arrived");
						return Status.Success;
					}
				}

				return Status.Running;
			}

			protected override void OnStop()
			{
				navMeshAgent.Stop();
			}

			private void RecalculatePath()
			{
				if (toGameObject)
				{
					Vector3 target = toGameObject.transform.position;
					targetPosAtLastPathing = target;
					navMeshAgent.CalculatePath(target, path);

					if (path.status == NavMeshPathStatus.PathComplete)
					{
						// If the actual end position is too far away from the desired
						// end position we consider our movement a failure.
						Vector3 pathEnd = path.corners[path.corners.Length - 1];
						if (GroundDistance(target, pathEnd) < 0.1f)
						{
							navMeshAgent.SetPath(path);
							return;
						}
					}
				}

				// Failure
				path = new NavMeshPath();
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
	}
}