"""
.meta fuer Sprite-Streifen, die unsere Python-Werkzeuge zeichnen.

Ein Streifen = n gleich grosse Bilder nebeneinander, Point-Filter, keine
Kompression. Jeder Aufruf wuerfelt neue guid/spriteIDs - also nur schreiben,
wenn noch keine .meta existiert, sonst verlieren Prefabs ihre Verweise.
"""

import random
import uuid

META_HEAD = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
{idtable}  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
{sprites}    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
{nametable}  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SPRITE = """    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: 0
        width: {w}
        height: {h}
      alignment: 9
      pivot: {{x: {px}, y: {py}}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sid}
      internalID: {iid}
      vertices: []
      indices:
      edges: []
      weights: []
"""



def write_strip_meta(path, base, frames, w, h, ppu, pivot=(0.5, 0.5)):
    rng = random.Random()
    names = ["%s_%d" % (base, i) for i in range(frames)]
    ids = [rng.randint(1 << 60, 1 << 62) for _ in names]
    idtable = "".join("  - first:\n      213: %d\n    second: %s\n" % (i, n) for n, i in zip(names, ids))
    sprites = "".join(SPRITE.format(name=n, x=k * w, w=w, h=h, px=pivot[0], py=pivot[1],
                                    sid=uuid.uuid4().hex, iid=i)
                      for k, (n, i) in enumerate(zip(names, ids)))
    nametable = "".join("      %s: %d\n" % (n, i) for n, i in sorted(zip(names, ids)))
    with open(path, "w", newline="\n") as fh:
        fh.write(META_HEAD.format(guid=uuid.uuid4().hex, ppu=ppu, idtable=idtable,
                                  sprites=sprites, nametable=nametable))
