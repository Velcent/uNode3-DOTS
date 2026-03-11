using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;
using System.Collections;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.UIElements;
using MaxyGames.UNode.Nodes;

namespace MaxyGames.UNode.Editors {
	class SystemGraphManipulator : GraphManipulator {
		public override bool IsValid(string action) {
			return graph is ECSGraph;
		}

		public override bool HandleCommand(string command) {
			if(command == Command.CompileCurrentGraph) {
				Compile();
				return true;
			}
			else if(command == Command.CompileGraph) {
				Compile();
				return true;
			}
			return base.HandleCommand(command);
		}

		private void Compile() {
			SystemCompiler.GenerateAndCompileGraphs();
		}
	}

	class ECSValueAssignator : DefaultValueAssignator {
		public override int order => int.MinValue;

		public override bool Process(ValueInput port, Type type, FilterAttribute filter) {
			if(type == typeof(AllocatorManager.AllocatorHandle)) {
				port.AssignToDefault(Allocator.Temp);
				return true;
			}
			else if(type == typeof(Entity)) {
				port.AssignToDefault(MemberData.CreateFromMember(typeof(Entity).GetMemberCached(nameof(Entity.Null))));
				return true;
			}
			else if(type == typeof(EntityManager)) {
				port.AssignToDefault(MemberData.None);
				return true;
			}
			else if(type == typeof(EntityCommandBuffer)) {
				port.AssignToDefault(MemberData.None);
				return true;
			}
			else if(type.IsValueType) {
				if(type.IsCastableTo(typeof(IComponentData))) {
					port.AssignToDefault(MemberData.None);
					return true;
				}
				if(type.IsCastableTo(typeof(IBufferElementData))) {
					port.AssignToDefault(MemberData.None);
					return true;
				}
			}
			return false;
		}
	}
}