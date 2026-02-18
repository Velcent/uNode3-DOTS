using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
	[NodeMenu("ECS/Flow", "Add Component", scope = NodeScope.ECSGraphAndJob)]
	public class AddComponent : FlowNode {
		[Filter(typeof(IComponentData), DisplayAbstractType =false)]
		public SerializedType componentType;
		public ECSLogicExecutionMode executionMode = ECSLogicExecutionMode.Auto;

		[NonSerialized]
		public ValueInput entity;
		[NonSerialized]
		public ValueInput component;
		[NonSerialized]
		public ValueInput entityManager;
		[NonSerialized]
		public ValueInput entityCommandBuffer;
		[NonSerialized]
		public ValueInput parallelWriter;
		[NonSerialized]
		public ValueInput sortKey;

		protected override void OnExecuted(Flow flow) {
			throw new Exception("ECS is not supported in reflection mode.");
		}

		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
			entity.SetTooltip("The entity to add component to.");
			component = ValueInput(nameof(component), () => componentType ?? typeof(IComponentData));
			component.MarkAsOptional();
			component.SetTooltip("The component value to sets, will use default value when empty.");

			switch(executionMode) {
				case ECSLogicExecutionMode.Run: {
					entityManager = ValueInput(nameof(entityManager), typeof(EntityManager));
					break;
				}
				case ECSLogicExecutionMode.Schedule: {
					entityCommandBuffer = ValueInput(nameof(entityCommandBuffer), typeof(EntityCommandBuffer));
					break;
				}
				case ECSLogicExecutionMode.ScheduleParallel: {
					parallelWriter = ValueInput(nameof(parallelWriter), typeof(EntityCommandBuffer.ParallelWriter));
					sortKey = ValueInput(nameof(sortKey), typeof(int));
					break;
				}
			}
		}

		public override void OnGeneratorInitialize() {
			base.OnGeneratorInitialize();
			if(executionMode == ECSLogicExecutionMode.Auto) {
				ECSGraphUtility.GetECSCommand(this, out var entities, out var commandName, out var commandType);
				//if(entities != null) {
				//	CG.RegisterUserObject(entities, this);
				//}
				if(commandType == typeof(EntityManager)) {
					CG.RegisterUserObject<Func<string>>(() => {
						if(component?.isAssigned == true) {
							if(ECSGraphUtility.IsFullyUnmanaged(component.ValueType)) {
								return CG.Flow(
									commandName.CGFlowInvoke(nameof(EntityManager.AddComponentData), new[] { component.ValueType }, entity.CGValue(), component.CGValue()),
									CG.FlowFinish(enter, exit)
								);
							}
							else {
								return CG.Flow(
									commandName.CGFlowInvoke(nameof(EntityManager.AddComponentObject), new[] { component.ValueType }, entity.CGValue(), component.CGValue()),
									CG.FlowFinish(enter, exit)
								);
							}
						}
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityManager.AddComponent), new[] { componentType.type }, entity.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}, ("ecb", this));
				}
				else if(commandType == typeof(EntityCommandBuffer)) {
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.AddComponent), 
								new[] { component.isAssigned ? component.ValueType : componentType.type }, entity.CGValue(), 
								component?.isAssigned == true ? component.CGValue() : null),
							CG.FlowFinish(enter, exit)
						);
					}, ("ecb", this));
				}
				else if(commandType == typeof(EntityCommandBuffer.ParallelWriter)) {
					var jobEntity = entities as IJobEntityContainer;
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.ParallelWriter.AddComponent),
								new[] { component.isAssigned ? component.ValueType : componentType.type },
								CG.GetVariableName(jobEntity.chunkIndexInQuery), entity.CGValue(), component?.isAssigned == true ? component.CGValue() : null),
							CG.FlowFinish(enter, exit)
						);
					}, ("ecb", this));
				}
				else {
					throw new Exception("Invalid context of node with Auto execution mode. It should be used inside a system On Update event, IJobEntity or IJobChunk graph.");
				}
			}
		}

		protected override string GenerateFlowCode() {
			if(entity.isAssigned) {
				switch(executionMode) {
					case ECSLogicExecutionMode.Auto: {
						var func = CG.GetUserObject<Func<string>>(("ecb", this));
						return func?.Invoke();
					}
					case ECSLogicExecutionMode.Run: {
						var commandName = CG.GeneratePort(entityManager);
						if(component?.isAssigned == true) {
							if(ECSGraphUtility.IsFullyUnmanaged(component.ValueType)) {
								return CG.Flow(
									commandName.CGFlowInvoke(nameof(EntityManager.AddComponentData), 
										new[] { component.ValueType },
										entity.CGValue(), component.CGValue()),
									CG.FlowFinish(enter, exit)
								);
							}
							else {
								return CG.Flow(
									commandName.CGFlowInvoke(nameof(EntityManager.AddComponentObject), 
										new[] { component.ValueType }, 
										entity.CGValue(), component.CGValue()),
									CG.FlowFinish(enter, exit)
								);
							}
						}
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityManager.AddComponent), new[] { componentType.type }, entity.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}
					case ECSLogicExecutionMode.Schedule: {
						var commandName = CG.GeneratePort(entityCommandBuffer);
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.AddComponent), 
								new[] { component.isAssigned ? component.ValueType : componentType.type }, 
								entity.CGValue(), component?.isAssigned == true ? component.CGValue() : null),
							CG.FlowFinish(enter, exit)
						);
					}
					case ECSLogicExecutionMode.ScheduleParallel: {
						var commandName = CG.GeneratePort(parallelWriter);
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.ParallelWriter.AddComponent), 
								new[] { component.isAssigned ? component.ValueType : componentType.type }, 
								sortKey.CGValue(), entity.CGValue(), component?.isAssigned == true ? component.CGValue() : null),
							CG.FlowFinish(enter, exit)
						);
					}
				}
			}
			return CG.FlowFinish(enter, exit);
		}

		public override string GetTitle() {
			if(component?.isAssigned == true) {
				return $"Add Component: {component.ValueType.PrettyName()}";
			}
			if(componentType?.isAssigned == true) {
				return $"Add Component: {componentType.prettyName}";
			}
			return base.GetTitle();
		}

		public override string GetRichTitle() {
			if(component?.isAssigned == true) {
				return $"Add Component: {uNodeUtility.GetRichTypeName(component.ValueType.PrettyName(), false)}";
			}
			if(componentType?.isAssigned == true) {
				return $"Add Component: {componentType.GetRichName()}";
			}
			return base.GetRichTitle();
		}
	}
}