using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace MaxyGames.UNode.Editors {
	[CustomEditor(typeof(ECSReflectionAuthoring), true)]
	public class ECSReflectionAuthoringEditor : Editor {
		static FilterAttribute componentFilter;

		static ECSReflectionAuthoringEditor() {
			componentFilter = new() {
				ArrayManipulator = false,
				ValidateType = (type) => {
					if(type.IsValueType == false || !type.IsCastableTo(typeof(IComponentData)) || type.IsDefinedAttribute<UReflectionAuthoringAttribute>() == false || ECSGraphUtility.IsFullyUnmanaged(type) == false) {
						return false;
					}
					return true;
				}
			};
		}

		public override void OnInspectorGUI() {
			var asset = target as ECSReflectionAuthoring;
			uNodeGUIUtility.EditType(asset.componentType, new GUIContent("Component Type"), (type) => {
				asset.componentType = type;
				uNodeEditorUtility.MarkDirty(asset);
			}, componentFilter);

			if(asset.componentType.isAssigned) {
				var fields = ReflectionUtils.GetFieldsCached(asset.componentType);

				for(int x = 0; x < asset.variables.Count; x++) {
					if(!fields.Any((v) => v.Name == asset.variables[x].name)) {
						asset.variables.RemoveAt(x);
					}
				}

				for(int i = 0; i < fields.Length; i++) {
					var field = fields[i];
					var fieldType = field.FieldType;
					if(field.IsPublic == false || field.IsNotSerialized || field.IsDefinedAttribute<NativeContainerAttribute>()) {
						continue;
					}
					var fieldName = field.Name;
					ECSVariableReflectionAuthoring variable = null;
					foreach(var var in asset.variables) {
						if(var.name == fieldName) {
							variable = var;
							break;
						}
					}
					if(variable == null) {
						variable = new() {
							name = fieldName,
							serializedValue = new(ReflectionUtils.CreateInstance(fieldType), fieldType),
						};
						asset.variables.Add(variable);
					}
					if(fieldType == typeof(Entity)) {
						uNodeGUIUtility.EditValueLayouted(new GUIContent(fieldName), variable.serializedValue.value, typeof(GameObject), val => {
							variable.serializedValue.ChangeValue(val);
							uNodeEditorUtility.MarkDirty(asset);
						}, settings: new EditValueSettings() {
							drawDecorator = false,
							nullable = false,
						});
					}
					else {
						uNodeGUIUtility.EditValueLayouted(new GUIContent(fieldName), variable.serializedValue.value, fieldType, val => {
							variable.serializedValue.ChangeValue(val);
							uNodeEditorUtility.MarkDirty(asset);
						}, settings: new EditValueSettings() {
							drawDecorator = false,
							nullable = false,
						});
					}
				}
			}
			else {
				EditorGUILayout.HelpBox("Component type is null, mising or unasigned.", MessageType.Error);
			}
		}
	}
}
