import os

OUT = r"c:\Unity Projects\Final Year Project - V2\Assets\Scenes"

G_TMP      = "f4688fdb7df04437aeb418b961361dc5"
G_IMG      = "fe87c0e1cc204ed48ad3b37840f39efc"
G_RAWIMG   = "1344c3c82d62a2a41a3576d8abb8e3ea"
G_BTN      = "4e29b1a8efbd4b44bb3f3716e73f07ff"
G_EVTSYS   = "76c392e42b5098c458856cdf6ecaaaa1"
G_SINPUT   = "4f231c4fb786f3946a6b90b886c48677"
G_CSCALER  = "0cd44c1031e13a943bb63640046fad76"
G_GRAYR    = "dc42784cf147c0c48a680349fa168899"
G_URP      = "a79441f348de89743a2939f4d699eac1"
G_GM       = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
G_CRAFT    = "e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1"
G_EVAL     = "f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2"
G_FONT     = "8f586378b4e144a9851e7b34d9b748ee"

def f(base, off=0):
    return base + off

def SCENE_PREAMBLE():
    return """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_BakeOnSceneLoad: 0
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
"""

def GO(fid, name, comps, layer=5, active=1):
    c = "\n".join(f"  - component: {{fileID: {fid+i}}}" for i in comps)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{c}
  m_Layer: {layer}
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {active}
"""

def TR(fid, parent, children):
    ch = ("\n" + "\n".join(f"  - {{fileID: {c}}}" for c in children)) if children else " []"
    p = f"{{fileID: {parent}}}" if parent else "{fileID: 0}"
    return f"""--- !u!4 &{fid}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-1}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{ch}
  m_Father: {p}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

def RT(fid, parent, children, amin, amax, apos, sdelta, pivot=(0.5,0.5)):
    ch = ("\n" + "\n".join(f"  - {{fileID: {c}}}" for c in children)) if children else " []"
    p = f"{{fileID: {parent}}}" if parent else "{fileID: 0}"
    ax,ay = amin; bx,by = amax; px,py = apos; sx,sy = sdelta; vx,vy = pivot
    return f"""--- !u!224 &{fid}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-1}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{ch}
  m_Father: {p}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {ax}, y: {ay}}}
  m_AnchorMax: {{x: {bx}, y: {by}}}
  m_AnchoredPosition: {{x: {px}, y: {py}}}
  m_SizeDelta: {{x: {sx}, y: {sy}}}
  m_Pivot: {{x: {vx}, y: {vy}}}
"""

def CR(fid):
    return f"""--- !u!222 &{fid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-2}}}
  m_CullTransparentMesh: 1
"""

def IMG(fid, r=1,g=1,b=1,a=1, raycast=1):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-3}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_IMG}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
  m_RaycastTarget: {raycast}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

def RAWIMG(fid):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-3}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_RAWIMG}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Texture: {{fileID: 0}}
  m_UVRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
"""

def TMP(fid, text="", sz=18, cr=1,cg=1,cb=1,ca=1, ha=2, va=512, wrap=1):
    rgba = (int(ca*255)<<24)|(int(cr*255)<<16)|(int(cg*255)<<8)|int(cb*255)
    if rgba < 0: rgba += 2**32
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-3}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_TMP}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: {cr}, g: {cg}, b: {cb}, a: {ca}}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: {text}
  m_isRightToLeft: 0
  m_fontAsset: {{fileID: 11400000, guid: {G_FONT}, type: 2}}
  m_sharedMaterial: {{fileID: 2180264, guid: {G_FONT}, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: {rgba}
  m_fontColor: {{r: {cr}, g: {cg}, b: {cb}, a: {ca}}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: {sz}
  m_fontSizeBase: {sz}
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 10
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: {ha}
  m_VerticalAlignment: {va}
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: {wrap}
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 5, y: 0, z: 5, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
"""

def BTN(fid, img_fid, interactable=1):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid-4}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_BTN}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: {interactable}
  m_TargetGraphic: {{fileID: {img_fid}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
"""

def CAMERA():
    return f"""--- !u!1 &10000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 10000001}}
  - component: {{fileID: 10000002}}
  - component: {{fileID: 10000003}}
  - component: {{fileID: 10000004}}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &10000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 10000000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: -10}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!20 &10000002
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 10000000}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {{r: 0.1, g: 0.1, b: 0.12, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 0
  m_HDR: 1
  m_AllowMSAA: 0
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 0
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!81 &10000003
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 10000000}}
  m_Enabled: 1
--- !u!114 &10000004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 10000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_URP}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_RenderShadows: 1
  m_RequiresDepthTextureOption: 2
  m_RequiresOpaqueTextureOption: 2
  m_CameraType: 0
  m_Cameras: []
  m_RendererIndex: -1
  m_VolumeLayerMask:
    serializedVersion: 2
    m_Bits: 1
  m_VolumeTrigger: {{fileID: 0}}
  m_VolumeFrameworkUpdateModeOption: 2
  m_RenderPostProcessing: 0
  m_Antialiasing: 0
  m_AntialiasingQuality: 2
  m_StopNaN: 0
  m_Dithering: 0
  m_ClearDepth: 1
  m_AllowXRRendering: 1
  m_AllowHDROutput: 1
  m_UseScreenCoordOverride: 0
  m_ScreenSizeOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_ScreenCoordScaleBias: {{x: 0, y: 0, z: 0, w: 0}}
  m_RequiresDepthTexture: 0
  m_RequiresColorTexture: 0
  m_Version: 2
  m_TaaSettings:
    m_Quality: 3
    m_FrameInfluence: 0.1
    m_JitterScale: 1
    m_MipBias: 0
    m_VarianceClampScale: 0.9
    m_ContrastAdaptiveSharpening: 0
"""

def EVENT_SYSTEM():
    return f"""--- !u!1 &20000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 20000001}}
  - component: {{fileID: 20000002}}
  - component: {{fileID: 20000003}}
  m_Layer: 0
  m_Name: EventSystem
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &20000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 20000000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &20000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 20000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_EVTSYS}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_FirstSelected: {{fileID: 0}}
  m_sendNavigationEvents: 1
  m_DragThreshold: 10
--- !u!114 &20000003
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 20000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SINPUT}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_SendPointerHoverToParent: 1
  m_HorizontalAxis: Horizontal
  m_VerticalAxis: Vertical
  m_SubmitButton: Submit
  m_CancelButton: Cancel
  m_InputActionsPerSecond: 10
  m_RepeatDelay: 0.5
  m_ForceModuleActive: 0
"""

def GAMEMANAGER_GO():
    return f"""--- !u!1 &45000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 45000001}}
  - component: {{fileID: 45000002}}
  m_Layer: 0
  m_Name: GameManager
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &45000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 45000000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &45000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 45000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_GM}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  playerGold: 500
  playerReputation: 0
"""

def CANVAS(children_rts):
    ch = "\n".join(f"  - {{fileID: {c}}}" for c in children_rts)
    return f"""--- !u!1 &30000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 30000001}}
  - component: {{fileID: 30000002}}
  - component: {{fileID: 30000003}}
  - component: {{fileID: 30000004}}
  m_Layer: 5
  m_Name: Canvas
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &30000001
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 30000000}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0, y: 0, z: 0}}
  m_ConstrainProportionsScale: 0
  m_Children:
{ch}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 0}}
--- !u!223 &30000002
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 30000000}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
--- !u!114 &30000003
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 30000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_CSCALER}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: 1920, y: 1080}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
--- !u!114 &30000004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 30000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_GRAYR}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
"""

def SCENE_ROOTS(roots):
    r = "\n".join(f"  - {{fileID: {x}}}" for x in roots)
    return f"""--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
{r}
"""

# ─── Helper: make a simple image panel (GO + RT + CR + IMG) ───────────────────
def panel(base, name, parent_rt, children_rts, amin, amax, apos, sdelta,
          r=0.13, g=0.13, b=0.16, a=1.0, active=1):
    return (
        GO(base, name, [1,2,3], active=active) +
        RT(base+1, parent_rt, children_rts, amin, amax, apos, sdelta) +
        CR(base+2) +
        IMG(base+3, r, g, b, a)
    )

# Helper: TMP label (GO + RT + CR + TMP)
def label(base, name, parent_rt, amin, amax, apos, sdelta,
          text="", sz=18, cr=1,cg=1,cb=1,ca=1, ha=2, va=512, wrap=1):
    return (
        GO(base, name, [1,2,3]) +
        RT(base+1, parent_rt, [], amin, amax, apos, sdelta) +
        CR(base+2) +
        TMP(base+3, text, sz, cr, cg, cb, ca, ha, va, wrap)
    )

# Helper: RawImage element (GO + RT + CR + RAWIMG)
def rawimg_el(base, name, parent_rt, amin, amax, apos, sdelta):
    return (
        GO(base, name, [1,2,3]) +
        RT(base+1, parent_rt, [], amin, amax, apos, sdelta) +
        CR(base+2) +
        RAWIMG(base+3)
    )

# Helper: button with text child
def button_el(base, name, parent_rt, amin, amax, apos, sdelta,
              text="", sz=20,
              ir=0.2, ig=0.5, ib=0.8,     # image color
              tr=1,tg=1,tb=1,              # text color
              interactable=1, pivot=(0.5,0.5)):
    text_base = base + 100000
    result  = GO(base, name, [1,2,3,4])
    result += RT(base+1, parent_rt, [text_base+1], amin, amax, apos, sdelta, pivot)
    result += CR(base+2)
    result += IMG(base+3, ir, ig, ib)
    result += BTN(base+4, base+3, interactable)
    # text child
    result += GO(text_base, "Text", [text_base+1, text_base+2, text_base+3])
    result += RT(text_base+1, base+1, [], (0,0),(1,1),(0,0),(0,0))
    result += CR(text_base+2)
    result += TMP(text_base+3, text, sz, tr, tg, tb, 1.0)
    return result

# ─── CRAFTING SCENE ──────────────────────────────────────────────────────────

def make_crafting_scene():
    parts = [SCENE_PREAMBLE(), CAMERA(), EVENT_SYSTEM()]

    # IDs for all RTs we need
    BG   = 50000000
    LP   = 60000000   # LeftPanel
    LT   = 61000000   # LeftTitle
    CL   = 62000000   # CoresLabel
    CC   = 63000000   # CoresContainer
    WL   = 64000000   # WoodsLabel
    WC   = 65000000   # WoodsContainer
    CP   = 70000000   # CenterPanel
    CT   = 71000000   # CenterTitle
    S1   = 72000000   # Slot1 container
    S1N  = 72100000   # Slot1 Name text
    S1I  = 72200000   # Slot1 Image (RawImage)
    S1C  = 72300000   # Slot1 Clear button
    S2   = 73000000
    S2N  = 73100000
    S2I  = 73200000
    S2C  = 73300000
    S3   = 74000000
    S3N  = 74100000
    S3I  = 74200000
    S3C  = 74300000
    CFM  = 75000000   # Confirm button
    RP   = 90000000   # RightPanel (resultPanel)
    WRI  = 91000000   # WandResultImage
    WN   = 92000000   # WandNameText
    WD   = 93000000   # WandDescText
    WA   = 94000000   # WandAttribText
    PRO  = 95000000   # ProceedButton
    ST   = 100000000  # StatusText
    CMGR = 40000000   # CraftingManager GO
    GMGR = 45000000   # GameManager

    # Canvas children (direct)
    canvas_children = [BG+1, LP+1, CP+1, RP+1, ST+1]
    # LeftPanel children
    lp_children = [LT+1, CL+1, CC+1, WL+1, WC+1]
    # CenterPanel children
    cp_children = [CT+1, S1+1, S2+1, S3+1, CFM+1]
    # Slot children
    s1_children = [S1N+1, S1I+1, S1C+1]
    s2_children = [S2N+1, S2I+1, S2C+1]
    s3_children = [S3N+1, S3I+1, S3C+1]
    # Button text children (100000 offset)
    # S1C text at S1C+100000+1 = 72400001
    s1c_children = [S1C+100001]
    s2c_children = [S2C+100001]
    s3c_children = [S3C+100001]
    cfm_children = [CFM+100001]
    pro_children = [PRO+100001]
    # RightPanel children
    rp_children = [WRI+1, WN+1, WD+1, WA+1, PRO+1]

    # Canvas
    parts.append(CANVAS(canvas_children))

    # Background
    parts.append(panel(BG, "Background", 30000001, [],
        (0,0),(1,1),(0,0),(0,0), 0.08,0.08,0.10))

    # ── LEFT PANEL ──
    parts.append(panel(LP, "LeftPanel", 30000001, lp_children,
        (0,0),(0.333,1),(0,0),(0,0), 0.12,0.12,0.16))

    parts.append(label(LT, "LeftTitle", LP+1,
        (0,1),(1,1),(0,-30),(0,55), "INVENTORY", sz=24, ha=2))
    parts.append(label(CL, "CoresLabel", LP+1,
        (0,1),(1,1),(0,-80),(0,30), "CORES", sz=16, cr=0.8,cg=0.8,cb=0.8, ha=1))
    # CoresContainer: just an empty RT (no image) - used as inventory parent
    parts.append(GO(CC, "CoresContainer", [1]))
    parts.append(RT(CC+1, LP+1, [], (0,1),(1,1),(0,-270),(0,340)))
    parts.append(label(WL, "WoodsLabel", LP+1,
        (0,1),(1,1),(0,-460),(-10,30), "WOODS", sz=16, cr=0.8,cg=0.8,cb=0.8, ha=1))
    # WoodsContainer
    parts.append(GO(WC, "WoodsContainer", [1]))
    parts.append(RT(WC+1, LP+1, [], (0,1),(1,1),(0,-650),(0,340)))

    # ── CENTER PANEL ──
    parts.append(panel(CP, "CenterPanel", 30000001, cp_children,
        (0.333,0),(0.667,1),(0,0),(0,0), 0.10,0.10,0.13))

    parts.append(label(CT, "CenterTitle", CP+1,
        (0,1),(1,1),(0,-30),(0,55), "CRAFT WAND", sz=24, ha=2))

    # Slot1
    parts.append(panel(S1, "Slot1", CP+1, s1_children,
        (0.05,1),(0.95,1),(0,-180),(-0,180), 0.16,0.16,0.20))
    parts.append(label(S1N, "Slot1Name", S1+1,
        (0,1),(1,1),(0,-20),(0,35), "Core 1 (Required)", sz=15, ha=2))
    parts.append(rawimg_el(S1I, "Slot1Image", S1+1,
        (0.1,0.2),(0.9,0.9),(0,0),(0,0)))
    parts.append(button_el(S1C, "Slot1Clear", S1+1,
        (1,1),(1,1),(-20,-20),(40,30), "X",
        sz=16, ir=0.6,ig=0.2,ib=0.2, pivot=(1,1)))

    # Slot2
    parts.append(panel(S2, "Slot2", CP+1, s2_children,
        (0.05,1),(0.95,1),(0,-390),(-0,180), 0.16,0.16,0.20))
    parts.append(label(S2N, "Slot2Name", S2+1,
        (0,1),(1,1),(0,-20),(0,35), "Core 2 (Optional)", sz=15, ha=2))
    parts.append(rawimg_el(S2I, "Slot2Image", S2+1,
        (0.1,0.2),(0.9,0.9),(0,0),(0,0)))
    parts.append(button_el(S2C, "Slot2Clear", S2+1,
        (1,1),(1,1),(-20,-20),(40,30), "X",
        sz=16, ir=0.6,ig=0.2,ib=0.2, pivot=(1,1)))

    # Slot3
    parts.append(panel(S3, "Slot3", CP+1, s3_children,
        (0.05,1),(0.95,1),(0,-600),(-0,180), 0.16,0.16,0.20))
    parts.append(label(S3N, "Slot3Name", S3+1,
        (0,1),(1,1),(0,-20),(0,35), "Wood (Required)", sz=15, ha=2))
    parts.append(rawimg_el(S3I, "Slot3Image", S3+1,
        (0.1,0.2),(0.9,0.9),(0,0),(0,0)))
    parts.append(button_el(S3C, "Slot3Clear", S3+1,
        (1,1),(1,1),(-20,-20),(40,30), "X",
        sz=16, ir=0.6,ig=0.2,ib=0.2, pivot=(1,1)))

    # Confirm Button
    parts.append(button_el(CFM, "ConfirmButton", CP+1,
        (0.1,0),(0.9,0),(0,60),(0,55), "Confirm Wand",
        sz=20, ir=0.2,ig=0.6,ib=0.2, pivot=(0.5,0)))

    # ── RIGHT PANEL (resultPanel) ──
    parts.append(panel(RP, "RightPanel", 30000001, rp_children,
        (0.667,0),(1,1),(0,0),(0,0), 0.12,0.12,0.16))

    parts.append(rawimg_el(WRI, "WandResultImage", RP+1,
        (0.1,0.6),(0.9,1),(0,-150),(0,0)))
    parts.append(label(WN, "WandNameText", RP+1,
        (0,1),(1,1),(0,-310),(0,40), "", sz=20, ha=2))
    parts.append(label(WD, "WandDescText", RP+1,
        (0,1),(1,1),(0,-410),(0,120), "", sz=15, ha=2, wrap=1))
    parts.append(label(WA, "WandAttribText", RP+1,
        (0,1),(1,1),(0,-570),(0,200), "", sz=14, ha=1, wrap=1))
    parts.append(button_el(PRO, "ProceedButton", RP+1,
        (0.1,0),(0.9,0),(0,60),(0,55), "Proceed to Evaluation",
        sz=18, ir=0.2,ig=0.5,ib=0.8, pivot=(0.5,0)))

    # Status text
    parts.append(label(ST, "StatusText", 30000001,
        (0,0),(1,0),(0,20),(0,35), "Select materials.",
        sz=14, cr=0.7,cg=0.7,cb=0.7, ha=2, pivot=(0.5,0)))

    # ── CraftingManager GO ──
    cm_fid = CMGR
    parts.append(f"""--- !u!1 &{cm_fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {cm_fid+1}}}
  - component: {{fileID: {cm_fid+2}}}
  m_Layer: 0
  m_Name: CraftingManager
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    parts.append(f"""--- !u!4 &{cm_fid+1}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cm_fid}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    # CraftingManager MonoBehaviour
    # Inspector refs:
    # coreInventoryContainer -> CC+1
    # woodInventoryContainer -> WC+1
    # slot1Image -> S1I+3, slot1Name -> S1N+3, slot1Clear -> S1C+4
    # slot2Image -> S2I+3, slot2Name -> S2N+3, slot2Clear -> S2C+4
    # slot3Image -> S3I+3, slot3Name -> S3N+3, slot3Clear -> S3C+4
    # confirmButton -> CFM+4
    # wandResultImage -> WRI+3, wandNameText -> WN+3, wandDescText -> WD+3, wandAttribText -> WA+3
    # proceedButton -> PRO+4
    # resultPanel -> RP
    # statusText -> ST+3
    parts.append(f"""--- !u!114 &{cm_fid+2}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cm_fid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_CRAFT}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  openAIUrl: https://api.openai.com/v1/chat/completions
  comfyUIUrl: http://127.0.0.1:8000
  clipNodeId: 57:27
  kSamplerNodeId: 57:3
  coreInventoryContainer: {{fileID: {CC+1}}}
  woodInventoryContainer: {{fileID: {WC+1}}}
  slot1Image: {{fileID: {S1I+3}}}
  slot1Name: {{fileID: {S1N+3}}}
  slot1Clear: {{fileID: {S1C+4}}}
  slot2Image: {{fileID: {S2I+3}}}
  slot2Name: {{fileID: {S2N+3}}}
  slot2Clear: {{fileID: {S2C+4}}}
  slot3Image: {{fileID: {S3I+3}}}
  slot3Name: {{fileID: {S3N+3}}}
  slot3Clear: {{fileID: {S3C+4}}}
  confirmButton: {{fileID: {CFM+4}}}
  wandResultImage: {{fileID: {WRI+3}}}
  wandNameText: {{fileID: {WN+3}}}
  wandDescText: {{fileID: {WD+3}}}
  wandAttribText: {{fileID: {WA+3}}}
  proceedButton: {{fileID: {PRO+4}}}
  resultPanel: {{fileID: {RP}}}
  statusText: {{fileID: {ST+3}}}
""")

    parts.append(GAMEMANAGER_GO())
    parts.append(SCENE_ROOTS([10000001, 20000001, 30000001, cm_fid+1, 45000001]))

    return "".join(parts)


# ─── EVALUATION SCENE ────────────────────────────────────────────────────────

def make_eval_scene():
    parts = [SCENE_PREAMBLE(), CAMERA(), EVENT_SYSTEM()]

    # IDs
    BG   = 50000000
    LP   = 60000000
    LTI  = 61000000   # LeftTitle
    CNM  = 62000000   # CustomerName
    CSC  = 63000000   # CustomerSchool
    CPF  = 64000000   # CustomerProfession
    CPL  = 65000000   # CustomerPersonality
    CRQ  = 66000000   # CustomerRequest
    CGL  = 67000000   # CustomerGoal
    CCN  = 68000000   # CustomerConstraint
    CP   = 70000000   # CenterPanel
    CTI  = 71000000   # CenterTitle
    WIM  = 72000000   # WandImage (RawImage)
    WNM  = 73000000   # WandNameText
    WDS  = 74000000   # WandDescText
    WAT  = 75000000   # WandAttribText
    WMT  = 76000000   # WandMaterialsText
    RP   = 80000000   # RightPanel (resultPanel)
    RTI  = 81000000   # RightTitle
    SCR  = 82000000   # ScoreText
    VRD  = 83000000   # VerdictText
    WWK  = 84000000   # WhatWorked
    WMS  = 85000000   # WhatMissed
    CRC  = 86000000   # CustomerReaction
    GLD  = 87000000   # GoldEarned
    REP  = 88000000   # Reputation
    NCB  = 89000000   # NextCustomerButton
    STX  = 100000000  # StatusText
    EMGR = 40000000   # EvaluationManager GO
    GMGR = 45000000

    lp_children = [LTI+1, CNM+1, CSC+1, CPF+1, CPL+1, CRQ+1, CGL+1, CCN+1]
    cp_children = [CTI+1, WIM+1, WNM+1, WDS+1, WAT+1, WMT+1]
    rp_children = [RTI+1, SCR+1, VRD+1, WWK+1, WMS+1, CRC+1, GLD+1, REP+1, NCB+1]
    canvas_children = [BG+1, LP+1, CP+1, RP+1, STX+1]

    parts.append(CANVAS(canvas_children))

    # Background
    parts.append(panel(BG, "Background", 30000001, [],
        (0,0),(1,1),(0,0),(0,0), 0.08,0.08,0.10))

    # ── LEFT PANEL (customer dossier) ──
    parts.append(panel(LP, "LeftPanel", 30000001, lp_children,
        (0,0),(0.333,1),(0,0),(0,0), 0.12,0.12,0.16))
    parts.append(label(LTI, "LeftTitle", LP+1,
        (0,1),(1,1),(0,-30),(0,55), "CUSTOMER", sz=22, ha=2))

    dossier = [
        (CNM, "CustomerName",       "Name:"),
        (CSC, "CustomerSchool",     "School:"),
        (CPF, "CustomerProfession", "Profession:"),
        (CPL, "CustomerPersonality","Personality:"),
        (CRQ, "CustomerRequest",    "Request:"),
        (CGL, "CustomerGoal",       "True Goal:"),
        (CCN, "CustomerConstraint", "Constraint:"),
    ]
    y = -85
    for base, name, placeholder in dossier:
        parts.append(label(base, name, LP+1,
            (0,1),(1,1),(0, y),(-10, 65), placeholder,
            sz=14, cr=0.9,cg=0.9,cb=0.9, ha=1, wrap=1))
        y -= 70

    # ── CENTER PANEL (wand display) ──
    parts.append(panel(CP, "CenterPanel", 30000001, cp_children,
        (0.333,0),(0.667,1),(0,0),(0,0), 0.10,0.10,0.13))
    parts.append(label(CTI, "CenterTitle", CP+1,
        (0,1),(1,1),(0,-30),(0,55), "YOUR WAND", sz=22, ha=2))
    parts.append(rawimg_el(WIM, "WandImage", CP+1,
        (0.1,0.5),(0.9,1),(0,-165),(0,0)))
    parts.append(label(WNM, "WandNameText", CP+1,
        (0,1),(1,1),(0,-350),(0,45), "", sz=20, ha=2))
    parts.append(label(WDS, "WandDescText", CP+1,
        (0,1),(1,1),(0,-450),(-10,130), "", sz=14, ha=2, wrap=1))
    parts.append(label(WAT, "WandAttribText", CP+1,
        (0,1),(1,1),(0,-600),(-10,180), "", sz=13, ha=1, wrap=1))
    parts.append(label(WMT, "WandMaterialsText", CP+1,
        (0,1),(1,1),(0,-720),(-10,80), "", sz=13, ha=1, wrap=1))

    # ── RIGHT PANEL (results) ──
    parts.append(panel(RP, "RightPanel", 30000001, rp_children,
        (0.667,0),(1,1),(0,0),(0,0), 0.12,0.12,0.16))
    parts.append(label(RTI, "RightTitle", RP+1,
        (0,1),(1,1),(0,-30),(0,55), "EVALUATION", sz=22, ha=2))
    # Score - big text
    parts.append(label(SCR, "ScoreText", RP+1,
        (0,1),(1,1),(0,-110),(0,90), "0", sz=60, ha=2))
    parts.append(label(VRD, "VerdictText", RP+1,
        (0,1),(1,1),(0,-210),(-10,70), "", sz=16, ha=2, wrap=1))
    parts.append(label(WWK, "WhatWorkedText", RP+1,
        (0,1),(1,1),(0,-320),(-10,100), "", sz=13, ha=1, wrap=1))
    parts.append(label(WMS, "WhatMissedText", RP+1,
        (0,1),(1,1),(0,-440),(-10,100), "", sz=13, ha=1, wrap=1))
    parts.append(label(CRC, "CustomerReactionText", RP+1,
        (0,1),(1,1),(0,-555),(-10,80), "", sz=14, ha=2, wrap=1,
        cr=0.9,cg=0.85,cb=0.6))
    parts.append(label(GLD, "GoldEarnedText", RP+1,
        (0,1),(1,1),(0,-640),(0,55), "+0g", sz=24, ha=2, cr=0.9,cg=0.8,cb=0.2))
    parts.append(label(REP, "ReputationText", RP+1,
        (0,1),(1,1),(0,-700),(0,50), "+0 rep", sz=20, ha=2))
    parts.append(button_el(NCB, "NextCustomerButton", RP+1,
        (0.05,0),(0.95,0),(0,60),(0,55), "Next Customer",
        sz=20, ir=0.15,ig=0.45,ib=0.75, pivot=(0.5,0)))

    # Status text
    parts.append(label(STX, "StatusText", 30000001,
        (0,0),(1,0),(0,20),(0,35), "Evaluating...",
        sz=14, cr=0.7,cg=0.7,cb=0.7, ha=2, pivot=(0.5,0)))

    # ── EvaluationManager GO ──
    em_fid = EMGR
    parts.append(f"""--- !u!1 &{em_fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {em_fid+1}}}
  - component: {{fileID: {em_fid+2}}}
  m_Layer: 0
  m_Name: EvaluationManager
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    parts.append(f"""--- !u!4 &{em_fid+1}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {em_fid}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    parts.append(f"""--- !u!114 &{em_fid+2}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {em_fid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_EVAL}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  openAIUrl: https://api.openai.com/v1/chat/completions
  customerNameText: {{fileID: {CNM+3}}}
  customerSchoolText: {{fileID: {CSC+3}}}
  customerProfessionText: {{fileID: {CPF+3}}}
  customerPersonalityText: {{fileID: {CPL+3}}}
  customerRequestText: {{fileID: {CRQ+3}}}
  customerGoalText: {{fileID: {CGL+3}}}
  customerConstraintText: {{fileID: {CCN+3}}}
  wandImage: {{fileID: {WIM+3}}}
  wandNameText: {{fileID: {WNM+3}}}
  wandDescText: {{fileID: {WDS+3}}}
  wandAttribText: {{fileID: {WAT+3}}}
  wandMaterialsText: {{fileID: {WMT+3}}}
  resultPanel: {{fileID: {RP}}}
  scoreText: {{fileID: {SCR+3}}}
  verdictText: {{fileID: {VRD+3}}}
  whatWorkedText: {{fileID: {WWK+3}}}
  whatMissedText: {{fileID: {WMS+3}}}
  customerReactionText: {{fileID: {CRC+3}}}
  goldEarnedText: {{fileID: {GLD+3}}}
  reputationText: {{fileID: {REP+3}}}
  nextCustomerButton: {{fileID: {NCB+4}}}
  statusText: {{fileID: {STX+3}}}
""")

    parts.append(GAMEMANAGER_GO())
    parts.append(SCENE_ROOTS([10000001, 20000001, 30000001, em_fid+1, 45000001]))

    return "".join(parts)


# ─── Write files ─────────────────────────────────────────────────────────────

craft = make_crafting_scene()
evalu = make_eval_scene()

with open(os.path.join(OUT, "CraftingScene.unity"), "w", encoding="utf-8") as fh:
    fh.write(craft)
print(f"CraftingScene.unity written ({len(craft.splitlines())} lines)")

with open(os.path.join(OUT, "EvaluationScene.unity"), "w", encoding="utf-8") as fh:
    fh.write(evalu)
print(f"EvaluationScene.unity written ({len(evalu.splitlines())} lines)")
