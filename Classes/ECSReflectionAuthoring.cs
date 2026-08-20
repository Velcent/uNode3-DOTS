using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace MaxyGames.UNode {
	[AttributeUsage(AttributeTargets.Struct)]
	public class UReflectionAuthoringAttribute : Attribute { }

	public class ECSReflectionAuthoring : MonoBehaviour {
		public SerializedType componentType = SerializedType.None;
		public List<ECSVariableReflectionAuthoring> variables = new();


		public ECSVariableReflectionAuthoring GetVariable(string name) {
			return variables.FirstOrDefault(v => v.name == name);
		}

		public object GetVariableValue(string name) {
			return variables.FirstOrDefault(v => v.name == name)?.serializedValue.value;
		}

		public T GetVariableValue<T>(string name) {
			var var = variables.FirstOrDefault(v => v.name == name);
			if(var != null && var.serializedValue.value is T result) {
				return result;
			}
			return default;
		}

		class Baker : Baker<ECSReflectionAuthoring> {
			static MethodInfo addcomponentMethod;

			static Baker() {
				addcomponentMethod = typeof(Baker).GetMethod(nameof(DoAddComponent), MemberData.flags);
			}

			public override void Bake(ECSReflectionAuthoring authoring) {
				if(authoring.componentType.isAssigned) {
					var componentType = ReflectionUtils.GetNativeType(authoring.componentType.type);
					var entity = GetEntity(TransformUsageFlags.None);

					var componentValue = ReflectionUtils.CreateInstance(componentType);

					var fields = ReflectionUtils.GetFieldsCached(componentType);
					for(int i = 0; i < fields.Length; i++) {
						var field = fields[i];
						var fieldName = field.Name;
						foreach(var var in authoring.variables) {
							if(var.name == fieldName) {
								var value = var.Get();
								if(value is GameObject go) {
									if(var.type == typeof(Entity)) {
										value = GetEntity(go, TransformUsageFlags.None);
									}
								}
								field.SetValueOptimized(componentValue, value);
								break;
							}
						}
					}

					addcomponentMethod.MakeGenericMethod(componentType).InvokeOptimized(this, entity, componentValue);
				}
			}

			public void DoAddComponent<T>(Entity entity, T component) where T : unmanaged, IComponentData {
				AddComponent(entity, component);
			}
		}


		[NonSerialized]
		private object m_instance;
		[NonSerialized]
		private Type m_instanceType;
		private object instance {
			get {
				if(m_instanceType != componentType.nativeType) {
					m_instanceType = componentType.nativeType;
					m_instance = ReflectionUtils.CreateInstance(componentType.nativeType);
				}
				return m_instance;
			}
		}

		private void OnDrawGizmos() {
			if(componentType.isAssigned && instance is IAuthoringGizmosComponent authoringGizmos) {
				authoringGizmos.OnDrawGizmos(this);
			}
		}

		private void OnDrawGizmosSelected() {
			if(componentType.isAssigned && instance is IAuthoringGizmosComponent authoringGizmos) {
				authoringGizmos.OnDrawGizmos(this);
			}
		}
	}

	public interface IAuthoringGizmosComponent {
		void OnDrawGizmos(ECSReflectionAuthoring authoring);
		void OnDrawGizmosSelected(ECSReflectionAuthoring authoring);
	}

	[Serializable]
	public class ECSVariableReflectionAuthoring : IValue {
		public string name;
		public SerializedValue serializedValue = new();

		public System.Type type {
			get {
				if(serializedValue != null) {
					return serializedValue.type;
				}
				return typeof(object);
			}
			set {
				if(serializedValue != null) {
					serializedValue.type = value;
				}
				else {
					serializedValue = new SerializedValue(null, value);
				}
			}
		}

		public object Get() {
			return serializedValue.value;
		}

		public void Set(object value) {
			serializedValue = new SerializedValue(value);
		}
	}
}
