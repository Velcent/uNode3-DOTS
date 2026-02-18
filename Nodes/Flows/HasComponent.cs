using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Data", "Has Component", scope = NodeScope.ECSGraph)]
    public class HasComponent : ValueNode {
		[Filter(typeof(IComponentData), DisplayAbstractType = false)]
		public SerializedType componentType;

		public enum ExecutionKind {
			Auto,
			SystemAPI,
			EntityManager,
		}

		public ExecutionKind executionKind = ExecutionKind.Auto;

		private const PortAccessibility accessibility = PortAccessibility.ReadOnly;

		[NonSerialized]
		public ValueInput entity;
		[NonSerialized]
		public ValueInput entityManager;
		[NonSerialized]
		public ValueInput componentLookup;

		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
			switch(executionKind) {
				case ExecutionKind.EntityManager: {
					entityManager = ValueInput(nameof(entityManager), typeof(EntityManager));
					break;
				}
			}
		}

		protected override Type ReturnType() => typeof(bool);

		public override string GetRichTitle() {
			return $"Has Component: {componentType.GetRichName()}";
		}

		public override void OnGeneratorInitialize() {
			base.OnGeneratorInitialize();
			if(executionKind == ExecutionKind.Auto) {
				ECSGraphUtility.GetECSCommand(this, out var entities, out var commandName, out var commandType, autoRegisterVariableInJob: false, isValue: true);
				if(commandType == typeof(EntityManager)) {
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Invoke(typeof(SystemAPI), nameof(SystemAPI.GetComponent), new[] { componentType.type }, entity.CGValue());
					}, ("ecb", this));
				}
				else if(commandType == typeof(EntityCommandBuffer) || commandType == typeof(EntityCommandBuffer.ParallelWriter)) {
					var variables = entities.JobVariables;
					var lookupType = typeof(ComponentLookup<>).MakeGenericType(componentType);
					if(variables != null) {
						var nm = ECSGraphUtility.GetComponentLookup(commandType, variables, accessibility);
						CG.RegisterUserObject<Func<string>>(() => {
							return nm.CGInvoke(nameof(ComponentLookup<Asset>.HasComponent), entity.CGValue());
						}, ("ecb", this));
					}
				}
				else {
					throw new Exception("Invalid context of node with Auto execution mode. It should be used inside a system On Update event, IJobEntity or IJobChunk graph.");
				}
			}
		}

		protected override string GenerateValueCode() {
			if(executionKind == ExecutionKind.Auto) {
				var func = CG.GetUserObject<Func<string>>(("ecb", this));
				return func?.Invoke();
			}
			else if(executionKind == ExecutionKind.SystemAPI) {
				return typeof(SystemAPI).CGInvoke(nameof(SystemAPI.HasComponent), new[] { componentType.type }, entity.CGValue());
			}
			else if(executionKind == ExecutionKind.EntityManager) {
				return entityManager.CGValue().CGInvoke(nameof(EntityManager.HasComponent), new[] { componentType.type }, entity.CGValue());
			}
			return null;
		}
	}
}