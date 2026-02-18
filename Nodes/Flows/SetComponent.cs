using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Flow", "Set Component", scope = NodeScope.ECSGraph)]
    public class SetComponent : FlowNode {
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
			entity.SetTooltip("The entity to set component to.");
			component = ValueInput(nameof(component), typeof(IComponentData));
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
				if(commandType == typeof(EntityManager)) {
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityManager.SetComponentData), 
								new[] { component.ValueType }, 
								entity.CGValue(), component.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}, ("ecb", this));
				}
				else if(commandType == typeof(EntityCommandBuffer)) {
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.SetComponent),
								new[] { component.ValueType }, 
								entity.CGValue(), component.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}, ("ecb", this));
				}
				else if(commandType == typeof(EntityCommandBuffer.ParallelWriter)) {
					var jobEntity = entities as IJobEntityContainer;
					CG.RegisterUserObject<Func<string>>(() => {
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.ParallelWriter.SetComponent),
								new[] { component.ValueType },
								CG.GetVariableName(jobEntity.chunkIndexInQuery), entity.CGValue(), component.CGValue()),
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
			if(entity.isAssigned && component.isAssigned) {
				switch(executionMode) {
					case ECSLogicExecutionMode.Auto: {
						var func = CG.GetUserObject<Func<string>>(("ecb", this));
						return func?.Invoke();
					}
					case ECSLogicExecutionMode.Run: {
						var commandName = CG.GeneratePort(entityManager);
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityManager.SetComponentData), new[] { component.ValueType }, entity.CGValue(), component.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}
					case ECSLogicExecutionMode.Schedule: {
						var commandName = CG.GeneratePort(entityCommandBuffer);
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.SetComponent),
								new[] { component.ValueType },
								entity.CGValue(), component.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}
					case ECSLogicExecutionMode.ScheduleParallel: {
						var commandName = CG.GeneratePort(parallelWriter);
						return CG.Flow(
							commandName.CGFlowInvoke(nameof(EntityCommandBuffer.ParallelWriter.SetComponent),
								new[] { component.ValueType },
								sortKey.CGValue(), entity.CGValue(), component.CGValue()),
							CG.FlowFinish(enter, exit)
						);
					}
				}
			}
			return CG.FlowFinish(enter, exit);
		}

		public override void CheckError(ErrorAnalyzer analyzer) {
			base.CheckError(analyzer);
			if(component.isAssigned) {
				if(component.ValueType.IsValueType == false) {
					analyzer.RegisterError(this, "Component must be assigned to Value Type ( struct )");
				}
			}
		}
	}
}