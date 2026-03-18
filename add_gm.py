SCENE = r"c:\Unity Projects\Final Year Project - V2\Assets\Scenes\CustomerGeneratorTest.unity"
G_GM  = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"

GM_YAML = """--- !u!1 &45000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 45000001}
  - component: {fileID: 45000002}
  m_Layer: 0
  m_Name: GameManager
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &45000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 45000000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &45000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 45000000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: """ + G_GM + """, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  playerGold: 500
  playerReputation: 0
"""

with open(SCENE, "r", encoding="utf-8") as fh:
    content = fh.read()

# Insert GM YAML before SceneRoots
SENTINEL = "--- !u!1660057539 &9223372036854775807"
content = content.replace(SENTINEL, GM_YAML + SENTINEL)

# Add to SceneRoots
content = content.replace(
    "  - {fileID: 40000001}\n",
    "  - {fileID: 40000001}\n  - {fileID: 45000001}\n"
)

with open(SCENE, "w", encoding="utf-8") as fh:
    fh.write(content)

print(f"Done — {len(content.splitlines())} lines")
