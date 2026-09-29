import * as THREE from 'three'
import { buildWaveMesh, type WaveMeshData } from '../../lib/waveMesh'

/**
 * A browser port of the WavyBoard surf wave: the same 15-point cross-section, the same peel, the same per-vertex
 * shading attributes (thin water, foam, tube occlusion) and the palette of the game's Water material.
 * The camera sits on the shoulder and rides along with the curl, looking into the barrel.
 */

// Water_Wave.mat colours (linear in the game, used as display colours here)
// Unity material colours are authored in sRGB and used linear: convert the same way
const srgb = (r: number, g: number, b: number) => new THREE.Color().setRGB(r, g, b, THREE.SRGBColorSpace)
const DEEP = srgb(0.02, 0.12, 0.22)
const SHALLOW = srgb(0.05, 0.42, 0.58)
const SSS = srgb(0.2, 0.8, 0.9)
const FOAM = srgb(0.94, 0.97, 1.0)

const H = 4.2
const PEEL_SPEED = 6.2
const CELERITY = 7.5

const SWELLS: [number, number, number, number][] = [
  // direction angle (rad, 0 = toward the beach), wavelength (m), amplitude (m), phase
  [0.05, 78, 0.34, 0.0],
  [-0.35, 47, 0.2, 1.3],
  [0.42, 29, 0.12, 2.1],
  [-0.8, 13.5, 0.06, 0.4],
  [1.1, 8.2, 0.035, 2.8],
  [-1.3, 5.1, 0.02, 1.7],
]

const COMMON = /* glsl */ `
uniform float uTime;
uniform vec3 uSunDir;
uniform vec3 uSunColor;
uniform vec3 uZenith;
uniform vec3 uHorizon;
uniform vec2 uFlow;
uniform vec4 uSwellA[6];
uniform vec4 uSwellB[6];

vec3 swellDisplace(vec2 x0, float dist) {
  vec3 d = vec3(0.0);
  vec2 q = x0 + uFlow * uTime;
  for (int i = 0; i < 6; i++) {
    vec4 a = uSwellA[i];
    vec4 b = uSwellB[i];
    float th = a.z * dot(a.xy, q) - b.x * uTime + b.y;
    float lod = clamp(2.0 - 2.0 * dist / max(b.w, 1.0), 0.0, 1.0);
    d.xz += a.xy * (b.z * cos(th) * lod);
    d.y += a.w * sin(th) * lod;
  }
  return d;
}

vec3 swellNormal(vec2 x0, float dist, out float crest) {
  vec3 n = vec3(0.0, 1.0, 0.0);
  float h = 0.0;
  float amp = 1e-4;
  vec2 q = x0 + uFlow * uTime;
  for (int i = 0; i < 6; i++) {
    vec4 a = uSwellA[i];
    vec4 b = uSwellB[i];
    float th = a.z * dot(a.xy, q) - b.x * uTime + b.y;
    float lod = clamp(1.5 - dist / max(b.w * 8.0, 1.0), 0.0, 1.0);
    float wa = a.z * a.w * lod;
    n.x -= a.x * wa * cos(th);
    n.z -= a.y * wa * cos(th);
    h += a.w * sin(th) * lod;
    amp += a.w * lod;
  }
  crest = clamp(h / amp * 0.5 + 0.5, 0.0, 1.0);
  return normalize(n);
}

vec3 skyColor(vec3 d) {
  float h = clamp(d.y, 0.0, 1.0);
  vec3 col = mix(uHorizon, uZenith, pow(h, 0.5));
  float sd = max(dot(d, uSunDir), 0.0);
  col += uSunColor * (pow(sd, 1400.0) * 30.0 + pow(sd, 60.0) * 0.5 + pow(sd, 6.0) * 0.18);
  return col;
}
`

const WATER_FRAG = /* glsl */ `
uniform vec3 uDeep;
uniform vec3 uShallow;
uniform vec3 uSSS;
uniform vec3 uFoam;
uniform float uFogDensity;
uniform float uSSSStrength;
varying vec3 vWorldPos;
varying vec3 vNormal;
varying vec4 vAttr;
varying vec2 vFlowUV;
varying vec2 vSeaXZ;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
  float s = 0.0;
  float a = 0.5;
  for (int i = 0; i < 4; i++) { s += a * vnoise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
  return s;
}
// value noise with its analytic derivatives (x: value, yz: gradient): one pass instead of three for a normal
vec3 noised(vec2 x) {
  vec2 i = floor(x);
  vec2 f = fract(x);
  vec2 u = f * f * (3.0 - 2.0 * f);
  vec2 du = 6.0 * f * (1.0 - f);
  float a = hash(i);
  float b = hash(i + vec2(1.0, 0.0));
  float c = hash(i + vec2(0.0, 1.0));
  float d = hash(i + vec2(1.0, 1.0));
  float k = a - b - c + d;
  return vec3(a + (b - a) * u.x + (c - a) * u.y + k * u.x * u.y, du * (vec2(b - a, c - a) + k * u.yx));
}
vec3 fbmd(vec2 p) {
  vec3 s = vec3(0.0);
  float a = 0.5;
  float fr = 1.0;
  for (int i = 0; i < 3; i++) {
    vec3 n = noised(p);
    s += vec3(a * n.x, a * fr * n.yz);
    p = p * 2.03 + vec2(1.7, 9.2);
    fr *= 2.03;
    a *= 0.5;
  }
  return s;
}

// small ripples: a sum of short gravity waves in 3D, their gradient perturbs the normal
vec3 rippleGrad(vec3 p, float t) {
  vec3 g = vec3(0.0);
  for (int i = 0; i < 9; i++) {
    float fi = float(i);
    float ang = fi * 2.39996 + 0.3 + sin(fi * 3.1) * 0.4;
    vec3 dir = normalize(vec3(cos(ang), 0.5 * sin(fi * 1.7), sin(ang)));
    float lambda = 0.22 + 2.6 * fract(fi * 0.618 + 0.13);
    float k = 6.2831853 / lambda;
    float w = sqrt(9.81 * k);
    float ph = k * dot(dir, p) - w * t + fi * 1.37 + 2.0 * sin(dot(p.xz, vec2(0.031, 0.047)) + fi);
    g += dir * (0.055 * cos(ph));
  }
  return g;
}

void main() {
  vec3 N = normalize(vNormal);
  if (!gl_FrontFacing) N = -N;
  vec3 toCam = cameraPosition - vWorldPos;
  float dist = length(toCam);
  vec3 V = toCam / dist;

  float faceK0 = vAttr.w;
  float crest;
  vec3 sn = swellNormal(vSeaXZ, dist, crest);
  // tilt the mesh normal by the swell slope (the swell rides on the wave too)
  N = normalize(N + vec3(sn.x, 0.0, sn.z) * mix(0.9, 0.25, faceK0));

  vec3 flowP = vWorldPos + vec3(uFlow.x, 0.0, uFlow.y) * uTime;
  vec3 g = rippleGrad(flowP, uTime);
  float fade = exp(-dist / 70.0);
  vec2 mp = flowP.xz * 1.7 + flowP.y * 0.6;
  vec3 nd = fbmd(mp);
  g += vec3(nd.y, 0.0, nd.z) * 0.33;
  g.y += (nd.x - 0.5) * 0.3;
  g = g - N * dot(g, N);
  N = normalize(N - g * (0.6 * fade + 0.1));

  float thin = vAttr.x;
  float foamAmt = vAttr.y;
  float ao = vAttr.z;
  float faceK = vAttr.w;

  vec3 L = uSunDir;
  float NdotV = clamp(dot(N, V), 0.0, 1.0);
  float NdotL = dot(N, L);
  float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);

  vec3 R = reflect(-V, N);
  R.y = max(R.y, 0.03);
  R = normalize(R);
  vec3 env = skyColor(R) * 0.7;

  vec3 Hh = normalize(L + V);
  float NdotH = clamp(dot(N, Hh), 0.0, 1.0);
  float rough = 0.012;
  float r2 = rough * rough;
  float dd = NdotH * NdotH * (r2 - 1.0) + 1.0;
  float ggx = r2 / (3.14159 * dd * dd + 1e-5);
  float fSun = 0.02 + 0.98 * pow(1.0 - clamp(dot(Hh, V), 0.0, 1.0), 5.0);
  vec3 spec = uSunColor * (ggx * fSun * clamp(NdotL, 0.0, 1.0) * 0.08 / max(NdotV, 0.1));

  vec3 skyAmb = mix(uHorizon, uZenith, 0.55);
  float thinAll = clamp(thin + crest * 0.12 * (1.0 - faceK), 0.0, 1.0);
  vec3 scatter = mix(uDeep, uShallow, thinAll);
  vec3 body = scatter * (skyAmb * 0.85 + uSunColor * (0.25 + 0.75 * clamp(NdotL, 0.0, 1.0)) * 0.55);

  vec3 Lt = normalize(L + N * 0.35);
  float back = pow(clamp(dot(V, -Lt), 0.0, 1.0), 3.0);
  vec3 sss = uSSS * thinAll * (uSunColor * back * uSSSStrength + skyAmb * 0.9 * thinAll);

  float occl = 1.0 - ao * 0.62;
  vec3 water = (body + sss * mix(1.0, 0.75, ao)) * occl;
  vec3 col = mix(water, env * mix(1.0, 0.3, ao), fresnel) + spec * (1.0 - ao);

  // foam: thresholded lace, never a smear
  if (foamAmt > 0.002) {
    vec2 fuv = vFlowUV * vec2(0.32, 0.9) + vec2(-uTime * 0.35, uTime * 0.9);
    float n1 = fbm(fuv);
    float n2 = fbm(fuv * 3.1 + 3.1 - uTime * 0.5);
    float lace = abs(fbm(fuv * 1.7 + 9.0) - 0.5) * 2.0;
    float fh = n1 * 0.55 + n2 * 0.25 + (1.0 - lace) * 0.2;
    float thr = 1.0 - foamAmt * 1.05;
    float cover = smoothstep(thr - 0.06, thr + 0.03, fh + foamAmt * 0.22);
    cover *= smoothstep(0.0, 0.08, foamAmt);
    cover *= mix(0.55, 1.0, smoothstep(0.35, 0.7, n2 + foamAmt * 0.4));
    // relief of the foam from its own height field (screen-space derivatives, as in the game)
    vec3 dpx = dFdx(vWorldPos);
    vec3 dpy = dFdy(vWorldPos);
    vec3 r1 = cross(dpy, N);
    vec3 r2 = cross(N, dpx);
    float det = dot(dpx, r1);
    vec3 grad = (dFdx(fh) * r1 + dFdy(fh) * r2) / (abs(det) > 1e-8 ? det : 1e-8);
    vec3 Nf = normalize(N - grad * 0.35 * clamp(1.0 - dist / 120.0, 0.0, 1.0));
    float fNdotL = clamp(dot(Nf, L), 0.0, 1.0);
    float cavity = mix(0.58, 1.0, clamp(fh * 1.4, 0.0, 1.0));
    vec3 foamLit = uFoam * cavity * (skyAmb * 0.72 + uSunColor * (fNdotL * 0.7 + 0.22) * 0.8)
                   + uSSS * uSunColor * back * 0.25 * (1.0 - fh);
    foamLit *= mix(1.0, occl, 0.6);
    col = mix(col, foamLit, clamp(cover, 0.0, 1.0));
  }

  vec3 fogCol = skyColor(normalize(vec3(-V.x, 0.02, -V.z)));
  float fog = 1.0 - exp(-dist * uFogDensity);
  col = mix(col, fogCol, fog);

  gl_FragColor = vec4(col, 1.0);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}
`

const WAVE_VERT = /* glsl */ `
attribute vec4 aAttr;
attribute vec2 aFlow;
varying vec3 vWorldPos;
varying vec3 vNormal;
varying vec4 vAttr;
varying vec2 vFlowUV;
varying vec2 vSeaXZ;
void main() {
  vec3 p = position;
  float dist = length(cameraPosition - p);
  p += swellDisplace(p.xz, dist);
  float churn = aAttr.y * (1.0 - aAttr.z);
  vec3 q = p * 0.55 + vec3(uFlow.x, 0.0, uFlow.y) * uTime * 0.55;
  float lump = sin(q.x * 1.3 + sin(q.z * 1.7 + uTime * 1.3)) * sin(q.z * 1.1 + q.x * 0.4 - uTime * 0.9) * 0.5 + 0.5;
  p += normal * churn * (0.12 + 0.55 * lump);
  vec4 wp = modelMatrix * vec4(p, 1.0);
  vWorldPos = wp.xyz;
  vNormal = normalize(mat3(modelMatrix) * normal);
  vAttr = aAttr;
  vFlowUV = aFlow;
  vSeaXZ = position.xz;
  gl_Position = projectionMatrix * viewMatrix * wp;
}
`

const SEA_VERT = /* glsl */ `
uniform vec2 uCenter;
varying vec3 vWorldPos;
varying vec3 vNormal;
varying vec4 vAttr;
varying vec2 vFlowUV;
varying vec2 vSeaXZ;
void main() {
  vec3 p = position + vec3(uCenter.x, 0.0, uCenter.y);
  float dist = length(cameraPosition.xz - p.xz);
  p += swellDisplace(p.xz, dist);
  p.y -= 0.06 + dist * 0.0015;
  vWorldPos = p;
  vNormal = vec3(0.0, 1.0, 0.0);
  vAttr = vec4(0.0);
  vFlowUV = (p.xz + uFlow * uTime) * 0.5;
  vSeaXZ = position.xz + uCenter;
  gl_Position = projectionMatrix * viewMatrix * vec4(p, 1.0);
}
`

const SKY_VERT = /* glsl */ `
varying vec3 vDir;
void main() {
  vDir = normalize(position);
  vec4 p = projectionMatrix * mat4(mat3(viewMatrix)) * vec4(position, 1.0);
  gl_Position = p.xyww;
}
`
const SKY_FRAG = /* glsl */ `
varying vec3 vDir;
void main() {
  vec3 d = normalize(vDir);
  vec3 col = skyColor(vec3(d.x, max(d.y, 0.0), d.z));
  // a soft band of haze on the horizon
  col = mix(col, uHorizon * 1.05, exp(-max(d.y, 0.0) * 18.0) * 0.5);
  gl_FragColor = vec4(col, 1.0);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}
`

const SPRAY_VERT = /* glsl */ `
attribute float aLife;
attribute float aSize;
varying float vLife;
varying float vSize;
uniform float uPixelRatio;
void main() {
  vLife = aLife;
  vSize = aSize;
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  gl_PointSize = aSize * uPixelRatio * (300.0 / -mv.z);
  gl_Position = projectionMatrix * mv;
}
`
const SPRAY_FRAG = /* glsl */ `
uniform vec3 uColor;
varying float vLife;
varying float vSize;
void main() {
  vec2 c = gl_PointCoord - 0.5;
  float r = length(c);
  float a = smoothstep(0.5, 0.0, r);
  float life = clamp(vLife, 0.0, 1.0);
  a *= smoothstep(0.0, 0.25, life) * smoothstep(1.0, 0.6, 1.0 - life) * 0.45 * mix(1.0, 0.22, smoothstep(0.7, 2.2, vSize));
  if (a < 0.01) discard;
  gl_FragColor = vec4(uColor, a);
  #include <colorspace_fragment>
}
`

function makeSeaGeometry() {
  // camera-centred polar mesh: fine under the viewer, coarse toward the horizon (as OceanSurface.cs)
  const rings = 150
  const segs = 220
  const r0 = 0.5
  const r1 = 5000
  const growth = Math.pow(r1 / r0, 1 / (rings - 1))
  const pos = new Float32Array((rings * segs + 1) * 3)
  let o = 0
  pos[o++] = 0
  pos[o++] = 0
  pos[o++] = 0
  for (let j = 0; j < rings; j++) {
    const r = r0 * Math.pow(growth, j)
    for (let i = 0; i < segs; i++) {
      const a = (i / segs) * Math.PI * 2
      pos[o++] = Math.cos(a) * r
      pos[o++] = 0
      pos[o++] = Math.sin(a) * r
    }
  }
  const idx: number[] = []
  for (let i = 0; i < segs; i++) idx.push(0, 1 + ((i + 1) % segs), 1 + i)
  for (let j = 0; j < rings - 1; j++) {
    for (let i = 0; i < segs; i++) {
      const a = 1 + j * segs + i
      const b = 1 + j * segs + ((i + 1) % segs)
      const c = a + segs
      const d = b + segs
      idx.push(a, b, c, b, d, c)
    }
  }
  const g = new THREE.BufferGeometry()
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3))
  g.setIndex(idx)
  return g
}

interface Spray {
  pos: Float32Array
  vel: Float32Array
  life: Float32Array
  maxLife: Float32Array
  size: Float32Array
  geo: THREE.BufferGeometry
  next: number
}

export interface WaveSceneOptions {
  /** 0..1, how far the camera leans toward the pointer */
  parallax?: number
  /** shoulder: outside, looking into the barrel (landing); tube: deep in the barrel, looking at the mouth (report) */
  view?: 'shoulder' | 'tube'
}

export class WaveScene {
  private renderer: THREE.WebGLRenderer
  private scene = new THREE.Scene()
  private camera: THREE.PerspectiveCamera
  private uniforms: Record<string, THREE.IUniform>
  private wave: WaveMeshData
  private spray: Spray
  private sprayMat: THREE.ShaderMaterial
  private seaMat: THREE.ShaderMaterial
  private last = 0
  private raf = 0
  private running = false
  private pointer = new THREE.Vector2()
  private pointerSmooth = new THREE.Vector2()
  private time = 0
  private parallax: number
  private view: 'shoulder' | 'tube'
  private disposed = false

  constructor(canvas: HTMLCanvasElement, opts: WaveSceneOptions = {}) {
    this.parallax = opts.parallax ?? 1
    this.view = opts.view ?? 'shoulder'
    // no MSAA: the wave is soft water and the pixel budget below does the smoothing for a fraction of the cost
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance', alpha: false, stencil: false })
    this.renderer.setPixelRatio(1)
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping
    this.renderer.toneMappingExposure = 0.92
    this.renderer.outputColorSpace = THREE.SRGBColorSpace

    this.camera = new THREE.PerspectiveCamera(52, 1, 0.1, 8000)

    // the tube view backlights the lip (sun ahead, on the beach side), so it glows like in the game
    const sunDir = (this.view === 'tube' ? new THREE.Vector3(0.45, 0.42, 0.85) : new THREE.Vector3(-0.42, 0.2, -1)).normalize()
    const swellA: THREE.Vector4[] = []
    const swellB: THREE.Vector4[] = []
    for (const [ang, lambda, amp, phase] of SWELLS) {
      const k = (2 * Math.PI) / lambda
      const dir = new THREE.Vector2(Math.sin(ang), Math.cos(ang))
      swellA.push(new THREE.Vector4(dir.x, dir.y, k, amp))
      const omega = Math.sqrt(9.81 * k)
      swellB.push(new THREE.Vector4(omega, phase, 0.55 * amp, lambda * 9))
    }

    this.uniforms = {
      uTime: { value: 0 },
      uSunDir: { value: sunDir },
      uSunColor: { value: srgb(1.0, 0.9, 0.74).multiplyScalar(2.6) },
      uZenith: { value: srgb(0.14, 0.4, 0.8) },
      uHorizon: { value: srgb(0.6, 0.76, 0.9) },
      uFlow: { value: new THREE.Vector2(PEEL_SPEED, CELERITY * 0.35) },
      uSwellA: { value: swellA },
      uSwellB: { value: swellB },
      uDeep: { value: DEEP },
      uShallow: { value: SHALLOW },
      uSSS: { value: SSS },
      uFoam: { value: FOAM },
      uFogDensity: { value: 0.0016 },
      uSSSStrength: { value: this.view === 'tube' ? 1.15 : 2.5 },
    }

    // sky
    const skyMat = new THREE.ShaderMaterial({
      uniforms: this.uniforms,
      vertexShader: SKY_VERT,
      fragmentShader: COMMON + SKY_FRAG,
      side: THREE.BackSide,
      depthWrite: false,
    })
    const sky = new THREE.Mesh(new THREE.SphereGeometry(10, 32, 16), skyMat)
    sky.frustumCulled = false
    sky.renderOrder = -1
    this.scene.add(sky)

    // the surf wave
    this.wave = buildWaveMesh({
      height: H,
      sMin: -95,
      sMax: 80,
      rows: 330,
      peelSpeed: PEEL_SPEED,
      celerity: CELERITY,
      tubeScale: 1.22,
      hold: 1.6,
    })
    const wg = new THREE.BufferGeometry()
    wg.setAttribute('position', new THREE.BufferAttribute(this.wave.positions, 3))
    wg.setAttribute('normal', new THREE.BufferAttribute(this.wave.normals, 3))
    wg.setAttribute('aAttr', new THREE.BufferAttribute(this.wave.attributes, 4))
    wg.setAttribute('aFlow', new THREE.BufferAttribute(this.wave.flow, 2))
    wg.setIndex(new THREE.BufferAttribute(this.wave.index, 1))
    const waveMat = new THREE.ShaderMaterial({
      uniforms: this.uniforms,
      vertexShader: COMMON + WAVE_VERT,
      fragmentShader: COMMON + WATER_FRAG,
      side: THREE.DoubleSide,
    })
    const waveMesh = new THREE.Mesh(wg, waveMat)
    waveMesh.frustumCulled = false
    this.scene.add(waveMesh)

    // the ambient sea
    this.seaMat = new THREE.ShaderMaterial({
      uniforms: { ...this.uniforms, uCenter: { value: new THREE.Vector2() } },
      vertexShader: COMMON + SEA_VERT,
      fragmentShader: COMMON + WATER_FRAG,
      polygonOffset: true,
      polygonOffsetFactor: 2,
      polygonOffsetUnits: 2,
    })
    const sea = new THREE.Mesh(makeSeaGeometry(), this.seaMat)
    sea.frustumCulled = false
    this.scene.add(sea)

    // spray where the lip lands and off the pitching crest
    const n = 3200
    const geo = new THREE.BufferGeometry()
    const spray: Spray = {
      pos: new Float32Array(n * 3),
      vel: new Float32Array(n * 3),
      life: new Float32Array(n),
      maxLife: new Float32Array(n),
      size: new Float32Array(n),
      geo,
      next: 0,
    }
    spray.pos.fill(-9999)
    geo.setAttribute('position', new THREE.BufferAttribute(spray.pos, 3))
    geo.setAttribute('aLife', new THREE.BufferAttribute(spray.life, 1))
    geo.setAttribute('aSize', new THREE.BufferAttribute(spray.size, 1))
    this.spray = spray
    this.sprayMat = new THREE.ShaderMaterial({
      uniforms: { uColor: { value: new THREE.Color(0.93, 0.97, 1.0) }, uPixelRatio: { value: this.renderer.getPixelRatio() } },
      vertexShader: SPRAY_VERT,
      fragmentShader: SPRAY_FRAG,
      transparent: true,
      depthWrite: false,
    })
    const points = new THREE.Points(geo, this.sprayMat)
    points.frustumCulled = false
    this.scene.add(points)

    // warm the spray up so the first frame already has a plume
    for (let i = 0; i < 90; i++) this.stepSpray(1 / 30)
  }

  private emit(x: number, y: number, z: number, vx: number, vy: number, vz: number, life: number, size: number) {
    const s = this.spray
    const i = s.next
    s.next = (s.next + 1) % s.life.length
    s.pos[i * 3] = x
    s.pos[i * 3 + 1] = y
    s.pos[i * 3 + 2] = z
    s.vel[i * 3] = vx
    s.vel[i * 3 + 1] = vy
    s.vel[i * 3 + 2] = vz
    s.life[i] = 1
    s.maxLife[i] = life
    s.size[i] = size
  }

  private stepSpray(dt: number) {
    const s = this.spray
    const landing = this.wave.lipLanding
    // the lip lands continuously along the tube: a curtain of spray in front of it
    const rate = 720
    const count = Math.floor(rate * dt + Math.random())
    for (let k = 0; k < count && landing.length; k++) {
      const l = landing[Math.floor(Math.random() * landing.length)]
      const fresh = 1 - Math.min(1, Math.max(0, (l.tau - 1.2) / 2.4))
      if (Math.random() > 0.35 + 0.65 * fresh) continue
      this.emit(
        l.s + (Math.random() - 0.5) * 0.8,
        0.2 + Math.random() * 0.4,
        l.x + Math.random() * 1.2,
        -PEEL_SPEED * 0.35 + (Math.random() - 0.5) * 2,
        2.5 + Math.random() * 5.5 * (0.5 + fresh),
        1.5 + Math.random() * 3.5,
        0.9 + Math.random() * 1.4,
        Math.random() < 0.22 ? 1.4 + Math.random() * 2.2 : 0.14 + Math.random() * 0.45,
      )
    }
    // offshore wind feathers the pitching crest
    const feather = Math.floor(210 * dt + Math.random())
    for (let k = 0; k < feather; k++) {
      const sAt = -2 + Math.random() * 12
      this.emit(
        sAt,
        H * (1.0 + Math.random() * 0.12),
        H * (0.1 + Math.random() * 0.35),
        -PEEL_SPEED * 0.2,
        1.2 + Math.random() * 1.8,
        -3 - Math.random() * 4,
        1.4 + Math.random() * 1.2,
        Math.random() < 0.3 ? 1.2 + Math.random() * 1.8 : 0.16 + Math.random() * 0.4,
      )
    }
    for (let i = 0; i < s.life.length; i++) {
      if (s.life[i] <= 0) continue
      s.life[i] -= dt / s.maxLife[i]
      if (s.life[i] <= 0) {
        s.pos[i * 3 + 1] = -9999
        continue
      }
      s.vel[i * 3 + 1] -= 9.81 * 0.55 * dt
      const drag = Math.exp(-0.9 * dt)
      s.vel[i * 3] *= drag
      s.vel[i * 3 + 2] *= drag
      s.pos[i * 3] += s.vel[i * 3] * dt
      s.pos[i * 3 + 1] += s.vel[i * 3 + 1] * dt
      s.pos[i * 3 + 2] += s.vel[i * 3 + 2] * dt
      s.size[i] += dt * 0.9
    }
    ;(s.geo.attributes.position as THREE.BufferAttribute).needsUpdate = true
    ;(s.geo.attributes.aLife as THREE.BufferAttribute).needsUpdate = true
    ;(s.geo.attributes.aSize as THREE.BufferAttribute).needsUpdate = true
  }

  /**
   * The canvas is drawn at a pixel budget, not at the screen's density: a full-screen water shader on a Retina
   * display would be ~5 million pixels a frame. The budget adapts to the frame rate the page actually gets.
   */
  private budget = 2.3e6
  private cssW = 1
  private cssH = 1
  private applyResolution() {
    const dpr = Math.min(window.devicePixelRatio || 1, 1.5, Math.sqrt(this.budget / Math.max(1, this.cssW * this.cssH)))
    const q = Math.max(0.5, Math.round(dpr * 20) / 20)
    if (q === this.renderer.getPixelRatio()) return
    this.renderer.setPixelRatio(q)
    this.renderer.setSize(this.cssW, this.cssH, false)
    this.sprayMat.uniforms.uPixelRatio.value = q
  }

  setSize(w: number, h: number) {
    this.cssW = w
    this.cssH = h
    this.renderer.setSize(w, h, false)
    this.applyResolution()
    this.camera.aspect = w / Math.max(1, h)
    // narrow screens: pull back so the barrel still fits
    this.camera.fov =
      this.view === 'tube'
        ? this.camera.aspect < 0.8 ? 88 : this.camera.aspect < 1.2 ? 78 : 66
        : this.camera.aspect < 0.8 ? 72 : this.camera.aspect < 1.2 ? 60 : 50
    this.camera.updateProjectionMatrix()
    this.render()
  }

  setPointer(x: number, y: number) {
    this.pointer.set(x, y)
  }

  private placeCamera(t: number) {
    this.pointerSmooth.lerp(this.pointer, 0.04)
    const px = this.pointerSmooth.x * this.parallax
    const py = this.pointerSmooth.y * this.parallax
    const narrow = this.camera.aspect < 1
    if (this.view === 'tube') {
      // deep in the barrel, riding with the curl, looking out at the mouth (the game's tube shot)
      const sway = Math.sin(t * 0.43) * 0.18
      this.camera.position.set(-13 + Math.sin(t * 0.17) * 0.8, 1.25 + sway + py * 0.25, 1.75 + px * 0.35)
      this.camera.lookAt(new THREE.Vector3(12, 1.9 + py * 0.8, 3.3 + px * 1.6))
      this.camera.rotateZ(0.05 + Math.sin(t * 0.3) * 0.015)
      ;(this.seaMat.uniforms.uCenter.value as THREE.Vector2).set(this.camera.position.x, this.camera.position.z)
      return
    }
    // on the shoulder, just in front of the face, looking back into the barrel; a slow bob like a rider trimming
    const bob = Math.sin(t * 0.55) * 0.35
    const drift = Math.sin(t * 0.21) * 1.6
    this.camera.position.set(narrow ? 22 : 15 + drift, 1.7 + bob + py * 0.7, (narrow ? 10 : 6.6) + px * 1.8)
    const target = new THREE.Vector3(-12 + px * 3, 2.6 + py * 0.6, 0.4)
    this.camera.lookAt(target)
    this.camera.rotateZ(-0.04 + px * 0.02)
    ;(this.seaMat.uniforms.uCenter.value as THREE.Vector2).set(this.camera.position.x, this.camera.position.z)
  }

  render() {
    if (this.disposed) return
    this.placeCamera(this.time)
    this.renderer.render(this.scene, this.camera)
  }

  // frame pacing: windows of ~60 rAF intervals drive the pixel budget
  private intervals: number[] = []
  private slowWindows = 0
  private startedAt = 0
  private throttled = false
  private skip = false

  /** Half frame rate while the hero is mostly scrolled away: the page gets the GPU back. */
  setThrottle(on: boolean) {
    this.throttled = on
  }

  private adapt(interval: number, now: number) {
    // the first seconds (fonts, images, first raster) say nothing about the GPU
    if (now - this.startedAt < 2500) return
    this.intervals.push(interval)
    if (this.intervals.length < 60) return
    const sorted = [...this.intervals].sort((x, y) => x - y)
    this.intervals.length = 0
    const median = sorted[30]
    const mean = sorted.reduce((x, y) => x + y, 0) / sorted.length
    // the display's own refresh interval, snapped to the usual rates (120, 90, 60 Hz)
    const refresh = median < 9.5 ? 1000 / 120 : median < 13 ? 1000 / 90 : 1000 / 60
    if (mean > refresh * 1.3) {
      if (++this.slowWindows >= 2 && this.budget > 0.9e6) {
        this.budget *= 0.8
        this.slowWindows = 0
        this.applyResolution()
      }
    } else {
      this.slowWindows = 0
      if (mean < refresh * 1.08 && this.budget < 2.3e6) {
        this.budget = Math.min(2.3e6, this.budget * 1.15)
        this.applyResolution()
      }
    }
  }

  private loop = () => {
    if (!this.running) return
    this.raf = requestAnimationFrame(this.loop)
    const now = performance.now()
    const interval = now - this.last
    if (this.throttled) {
      this.skip = !this.skip
      if (this.skip) return
    }
    const dt = Math.min(interval / 1000, 1 / 20)
    this.last = now
    if (!this.throttled) this.adapt(interval, now)
    this.time += dt
    this.uniforms.uTime.value = this.time
    this.stepSpray(dt)
    this.render()
  }

  start() {
    if (this.running || this.disposed) return
    this.running = true
    this.last = performance.now()
    this.startedAt = this.last
    this.intervals.length = 0
    this.raf = requestAnimationFrame(this.loop)
  }

  stop() {
    this.running = false
    cancelAnimationFrame(this.raf)
  }

  /** A still frame at a given time (reduced motion). */
  still(t = 3.2) {
    this.time = t
    this.uniforms.uTime.value = t
    this.render()
  }

  dispose() {
    this.stop()
    this.disposed = true
    this.scene.traverse((o) => {
      const m = o as THREE.Mesh
      m.geometry?.dispose()
      const mat = m.material as THREE.Material | undefined
      mat?.dispose()
    })
    this.renderer.dispose()
  }
}
