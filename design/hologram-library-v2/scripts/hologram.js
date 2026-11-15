/* Three.js r128 classic-script component. Supplied textures remain caller-owned.
 * Reference distinction: Pisketona's aura is lilac; Ritenia's is warm amber.
 * Pass the appropriate color; artwork stays in its original colors. No inferred
 * rarity, levels, stars, names or BP are drawn by this presentation component.
 */
(function (global) {
  'use strict';

  const VERTEX = `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `;

  const CHARACTER_FRAGMENT = `
    varying vec2 vUv;
    uniform sampler2D uMap;
    uniform vec2 uTexel;
    uniform vec4 uUvRect;
    uniform vec3 uColor;
    uniform float uTime, uReveal, uSelected, uMotion, uDetail, uSrgb;
    vec3 decodeColor(vec3 c) {
      vec3 lo = c / 12.92;
      vec3 hi = pow((c + 0.055) / 1.055, vec3(2.4));
      return mix(lo, hi, step(vec3(0.04045), c));
    }
    float alphaAt(vec2 offset) {
      vec2 p = uUvRect.xy + vUv * uUvRect.zw + offset * uTexel * 2.0;
      if (p.x < uUvRect.x || p.y < uUvRect.y ||
          p.x > uUvRect.x + uUvRect.z || p.y > uUvRect.y + uUvRect.w) return 0.0;
      return texture2D(uMap, p).a;
    }
    void main() {
      vec4 art = texture2D(uMap, uUvRect.xy + vUv * uUvRect.zw);
      float edge = max(max(alphaAt(vec2(1., 0.)), alphaAt(vec2(-1., 0.))),
                       max(alphaAt(vec2(0., 1.)), alphaAt(vec2(0., -1.))));
      if (uDetail > 0.5) {
        edge = max(edge, max(max(alphaAt(vec2(.7, .7)), alphaAt(vec2(-.7, .7))),
                             max(alphaAt(vec2(.7, -.7)), alphaAt(vec2(-.7, -.7)))));
      }
      float outline = max(0.0, edge - art.a) * (0.23 + uSelected * 0.13);
      float reveal = 1.0 - smoothstep(uReveal - 0.025, uReveal + 0.025, vUv.y);
      float foot = smoothstep(0.0, 0.018, vUv.y);
      float alpha = max(art.a * 0.985, outline) * reveal * foot;
      // Discard genuinely empty texels: the upright plane never becomes a card rectangle.
      if (alpha < 0.025) discard;
      vec3 rgb = mix(art.rgb, decodeColor(art.rgb), uSrgb);
      float scan = 1.0 - uMotion * 0.035 * (0.5 + 0.5 * sin(vUv.y * 340.0 - uTime * 4.0));
      rgb *= scan;
      float frontier = exp(-abs(vUv.y - uReveal) * 85.0) * step(uReveal, 1.0);
      rgb += uColor * frontier * art.a * 0.45;
      rgb = mix(uColor * 1.12, rgb, smoothstep(0.01, 0.22, art.a));
      gl_FragColor = vec4(rgb, alpha);
      #include <encodings_fragment>
    }
  `;

  const RING_FRAGMENT = `
    varying vec2 vUv;
    uniform vec3 uColor;
    uniform float uTime, uSelected, uReady, uPulse, uMotion;
    void main() {
      vec2 p = (vUv - .5) * 2.0;
      float r = length(p);
      float a = atan(p.y, p.x);
      float outer = exp(-pow((r - .78) * 110.0, 2.0));
      float soft = exp(-pow((r - .78) * 17.0, 2.0)) * .18;
      float inner = exp(-pow((r - .59) * 120.0, 2.0)) * .25;
      float tick = pow(max(0.0, cos(a * 32.0 + uTime * .16 * uMotion)), 22.0);
      tick *= smoothstep(.65, .69, r) * (1.0 - smoothstep(.72, .74, r)) * .46;
      float pulse = exp(-pow((r - (.12 + uPulse * .85)) * 40.0, 2.0));
      pulse *= (1.0 - uPulse) * step(.001, uPulse) * uMotion;
      float alpha = ((outer + soft + inner + tick) * (.15 + uSelected * .13)
                   + pulse * .25) * uReady;
      if (alpha < .002) discard;
      gl_FragColor = vec4(uColor, alpha);
      #include <encodings_fragment>
    }
  `;

  const BEAM_FRAGMENT = `
    varying vec2 vUv;
    uniform vec3 uColor;
    uniform float uTime, uReady, uSelected;
    void main() {
      float fade = pow(1.0 - vUv.y, 3.5) * smoothstep(0.0, .06, vUv.y);
      float band = .82 + .18 * sin(vUv.y * 44.0 - uTime * 2.2);
      gl_FragColor = vec4(uColor, fade * band * uReady * (.055 + uSelected * .035));
      #include <encodings_fragment>
    }
  `;

  const SHIMMER_FRAGMENT = `
    varying vec2 vUv;
    uniform vec3 uColor;
    uniform float uTime, uSelected, uReady;
    void main() {
      float d = vUv.x * .65 + vUv.y * .35;
      float band = exp(-pow((d - fract(uTime * .12) * 1.8 + .4) * 12.0, 2.0));
      gl_FragColor = vec4(uColor, band * (.035 + uSelected * .035) * uReady);
      #include <encodings_fragment>
    }
  `;

  const PARTICLE_VERTEX = `
    attribute vec4 aSeed;
    uniform float uTime, uHeight, uRadius, uSize;
    varying float vFade;
    void main() {
      float life = fract(aSeed.x + uTime * (.085 + aSeed.y * .07));
      float angle = aSeed.z * 6.2831853 + uTime * (.07 + aSeed.y * .06);
      float radius = sqrt(aSeed.w) * uRadius;
      vec3 p = vec3(cos(angle) * radius, .065 + life * uHeight, sin(angle) * radius);
      vec4 mv = modelViewMatrix * vec4(p, 1.0);
      gl_Position = projectionMatrix * mv;
      gl_PointSize = clamp(uSize * 240.0 / max(.25, -mv.z), 1.0, 9.0);
      vFade = sin(life * 3.14159265) * (.4 + .6 * aSeed.y);
    }
  `;

  const PARTICLE_FRAGMENT = `
    varying float vFade;
    uniform vec3 uColor;
    uniform float uReady, uSelected;
    void main() {
      vec2 p = gl_PointCoord * 2.0 - 1.0;
      float glow = exp(-dot(p, p) * 4.8) * (1.0 - smoothstep(.6, 1.0, length(p)));
      float alpha = glow * vFade * uReady * (.34 + uSelected * .12);
      if (alpha < .006) discard;
      gl_FragColor = vec4(mix(uColor, vec3(1.0), .38), alpha);
      #include <encodings_fragment>
    }
  `;

  function smooth(a, b, x) {
    const t = Math.max(0, Math.min(1, (x - a) / (b - a)));
    return t * t * (3 - 2 * t);
  }

  function positive(value, fallback) {
    return Number.isFinite(value) && value > 0 ? value : fallback;
  }

  function characterRect(bounds, width, height) {
    const full = {left: 0, top: 0, right: width, bottom: height};
    if (!bounds || !['left', 'top', 'right', 'bottom'].every(key => Number.isFinite(bounds[key]))) return full;
    const left = Math.max(0, Math.min(width, bounds.left));
    const top = Math.max(0, Math.min(height, bounds.top));
    const right = Math.max(0, Math.min(width, bounds.right));
    const bottom = Math.max(0, Math.min(height, bounds.bottom));
    return right > left && bottom > top ? {left, top, right, bottom} : full;
  }

  function roundedShape(T, w, h) {
    const s = new T.Shape(), x = -w / 2, y = -h / 2, r = Math.min(w, h) * .055;
    s.moveTo(x + r, y);
    s.lineTo(x + w - r, y); s.quadraticCurveTo(x + w, y, x + w, y + r);
    s.lineTo(x + w, y + h - r); s.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    s.lineTo(x + r, y + h); s.quadraticCurveTo(x, y + h, x, y + h - r);
    s.lineTo(x, y + r); s.quadraticCurveTo(x, y, x + r, y);
    return s;
  }

  class HologramActor {
    constructor(options = {}) {
      const T = global.THREE;
      if (!T) throw new Error('HologramActor requires THREE r128 to be loaded first.');
      if (!options.cardTexture || !options.cardTexture.isTexture ||
          !options.characterTexture || !options.characterTexture.isTexture) {
        throw new TypeError('HologramActor requires cardTexture and a transparent characterTexture.');
      }
      this._T = T;
      this._disposed = false;
      this._geometries = new Set();
      this._materials = new Set();
      this._height = positive(options.height, 2.2);
      this._cardWidth = positive(options.cardWidth, 1.25);
      this._cardHeight = positive(options.cardHeight, 1.82);
      this._revealAge = 0;
      this._clock = 0;
      this._lastElapsed = null;
      this._selected = 0;
      this._selectedTarget = 0;
      this._reducedMotion = !!options.reducedMotion;
      this._hologramVisible = true;
      this._color = new T.Color(options.color === undefined ? 0xc19af6 : options.color).convertSRGBToLinear();
      this._cameraPosition = new T.Vector3();
      this._centerPosition = new T.Vector3();
      this._parentQuaternion = new T.Quaternion();
      this._facingQuaternion = new T.Quaternion();
      this._worldUp = new T.Vector3(0, 1, 0);

      this.group = new T.Group();
      this.group.name = 'HologramActor';
      this.group.userData.hologramActor = this;

      const shape = roundedShape(T, this._cardWidth, this._cardHeight);
      const faceGeometry = this._ownGeometry(new T.ShapeGeometry(shape, 6));
      const p = faceGeometry.attributes.position, uv = faceGeometry.attributes.uv;
      for (let i = 0; i < p.count; i++) {
        uv.setXY(i, (p.getX(i) + this._cardWidth / 2) / this._cardWidth,
                    (p.getY(i) + this._cardHeight / 2) / this._cardHeight);
      }
      uv.needsUpdate = true;
      this._card = new T.Group();
      this._card.name = 'Horizontal source card';
      this._card.rotation.x = -Math.PI / 2;
      this._card.position.y = .008;
      this.group.add(this._card);
      const bodyGeometry = this._ownGeometry(new T.ExtrudeGeometry(shape, {
        depth: .023, bevelEnabled: false, curveSegments: 6, steps: 1
      }));
      const body = new T.Mesh(bodyGeometry, this._ownMaterial(new T.MeshBasicMaterial({color: 0x252439, toneMapped: false})));
      this._card.add(body);
      const face = new T.Mesh(faceGeometry, this._ownMaterial(new T.MeshBasicMaterial({
        map: options.cardTexture, transparent: true, alphaTest: .025, toneMapped: false
      })));
      face.position.z = .024;
      this._card.add(face);
      this._shimmerUniforms = this._effectUniforms();
      this._shimmer = new T.Mesh(faceGeometry, this._shader(SHIMMER_FRAGMENT, this._shimmerUniforms, true));
      this._shimmer.position.z = .025;
      this._shimmer.renderOrder = 1;
      this._card.add(this._shimmer);

      const radius = Math.max(this._cardWidth * .78, this._cardHeight * .54);
      this._ringUniforms = this._effectUniforms();
      this._ringUniforms.uPulse = {value: 0};
      this._ringUniforms.uMotion = {value: 1};
      const ring = new T.Mesh(this._ownGeometry(new T.PlaneGeometry(radius * 2.4, radius * 2.4)),
                              this._shader(RING_FRAGMENT, this._ringUniforms, true));
      ring.name = 'Soft projection ring';
      ring.rotation.x = -Math.PI / 2;
      ring.position.y = .005;
      ring.renderOrder = 1;
      this.group.add(ring);
      this._ring = ring;

      this._beamUniforms = this._effectUniforms();
      this._beam = new T.Mesh(this._ownGeometry(new T.CylinderGeometry(radius * .48, radius * .70,
        this._height * .68, 20, 1, true)), this._shader(BEAM_FRAGMENT, this._beamUniforms, true));
      this._beam.position.y = this._height * .34 + .035;
      this._beam.renderOrder = 1;
      this.group.add(this._beam);

      const image = options.characterTexture.image || {};
      const iw = positive(image.naturalWidth || image.videoWidth || image.width, 1);
      const ih = positive(image.naturalHeight || image.videoHeight || image.height, 1);
      // Optional top-origin pixel bounds include caller-provided alpha padding.
      // Crop in the shader; the supplied texture and its pixels remain untouched.
      const bounds = characterRect(options.characterBounds, iw, ih);
      const characterWidth = bounds.right - bounds.left;
      const characterHeight = bounds.bottom - bounds.top;
      this._characterUniforms = {
        uMap: {value: options.characterTexture},
        uTexel: {value: new T.Vector2(1 / iw, 1 / ih)},
        uUvRect: {value: new T.Vector4(bounds.left / iw, 1 - bounds.bottom / ih,
                                     characterWidth / iw, characterHeight / ih)},
        uColor: {value: this._color}, uTime: {value: 0},
        uReveal: {value: -.06}, uSelected: {value: 0}, uMotion: {value: 1},
        uDetail: {value: 1}, uSrgb: {value: options.characterTexture.encoding === T.sRGBEncoding ? 1 : 0}
      };
      this._billboard = new T.Group();
      this._billboard.name = 'World upright character billboard';
      this._billboard.position.y = .085 + this._height / 2;
      this.group.add(this._billboard);
      const characterMaterial = this._shader(CHARACTER_FRAGMENT, this._characterUniforms, false);
      characterMaterial.alphaTest = .025;
      this._character = new T.Mesh(this._ownGeometry(new T.PlaneGeometry(this._height * characterWidth / characterHeight, this._height)), characterMaterial);
      this._character.name = 'Transparent character cutout';
      this._character.renderOrder = 2;
      this._billboard.add(this._character);

      // Fixed GPU particle pool; low quality changes drawRange, never reallocates.
      const count = 60, seeds = new Float32Array(count * 4);
      let seed = ((Number(options.color) || 0xc19af6) ^ 0x9e3779b9) >>> 0;
      const random = () => {seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296;};
      for (let i = 0; i < seeds.length; i++) seeds[i] = random();
      const particleGeometry = this._ownGeometry(new T.BufferGeometry());
      particleGeometry.setAttribute('position', new T.BufferAttribute(new Float32Array(count * 3), 3));
      particleGeometry.setAttribute('aSeed', new T.BufferAttribute(seeds, 4));
      particleGeometry.boundingSphere = new T.Sphere(new T.Vector3(0, this._height / 2, 0), this._height + radius);
      this._particleUniforms = this._effectUniforms();
      this._particleUniforms.uHeight = {value: this._height * .98};
      this._particleUniforms.uRadius = {value: radius * .72};
      this._particleUniforms.uSize = {value: .10};
      const particleMaterial = this._shader(PARTICLE_FRAGMENT, this._particleUniforms, true, PARTICLE_VERTEX);
      this._particles = new T.Points(particleGeometry, particleMaterial);
      this._particles.name = 'Bounded projection motes';
      this._particles.renderOrder = 3;
      this.group.add(this._particles);
      this._animatedUniforms = [this._ringUniforms, this._beamUniforms,
                                this._shimmerUniforms, this._particleUniforms];

      this.setQuality(options.quality || 'high');
      this.setReducedMotion(this._reducedMotion);
    }

    _ownGeometry(value) { this._geometries.add(value); return value; }
    _ownMaterial(value) { this._materials.add(value); return value; }
    _effectUniforms() {
      return {uColor: {value: this._color}, uTime: {value: 0},
              uSelected: {value: 0}, uReady: {value: 0}};
    }
    _shader(fragmentShader, uniforms, additive, vertexShader = VERTEX) {
      const T = this._T;
      return this._ownMaterial(new T.ShaderMaterial({
        uniforms, vertexShader, fragmentShader, transparent: true,
        side: T.DoubleSide, depthTest: true, depthWrite: false, toneMapped: false,
        blending: additive ? T.AdditiveBlending : T.NormalBlending
      }));
    }

    reveal() {
      if (this._disposed) return this;
      this._revealAge = this._reducedMotion ? 2 : 0;
      this._characterUniforms.uReveal.value = this._reducedMotion ? 1.06 : -.06;
      return this;
    }

    setSelected(value) {
      this._selectedTarget = value ? 1 : 0;
      if (this._reducedMotion) this._selected = this._selectedTarget;
      return this;
    }

    setHologramVisible(value) {
      if (this._disposed) return this;
      this._hologramVisible = !!value;
      this._syncVisibility();
      return this;
    }

    _syncVisibility() {
      const visible = this._hologramVisible;
      const animated = visible && !this._reducedMotion;
      const high = this._quality === 'high';
      this._billboard.visible = visible;
      this._ring.visible = visible;
      this._particles.visible = animated;
      this._beam.visible = animated && high;
      this._shimmer.visible = animated && high;
      // The card group, its face and its body are independent of this switch.
    }

    setQuality(value) {
      if (this._disposed) return this;
      this._quality = value === 'low' ? 'low' : 'high';
      const high = this._quality === 'high';
      this._characterUniforms.uDetail.value = high ? 1 : 0;
      this._particles.geometry.setDrawRange(0, high ? 60 : 18);
      this._particleUniforms.uSize.value = high ? .10 : .085;
      this._syncVisibility();
      return this;
    }

    setReducedMotion(value) {
      if (this._disposed) return this;
      this._reducedMotion = !!value;
      const motion = this._reducedMotion ? 0 : 1;
      this._characterUniforms.uMotion.value = motion;
      this._ringUniforms.uMotion.value = motion;
      if (this._reducedMotion) {
        this._revealAge = 2;
        this._characterUniforms.uReveal.value = 1.06;
        this._character.position.y = 0;
        this._selected = this._selectedTarget;
        this._ringUniforms.uReady.value = 1;
        this._ringUniforms.uTime.value = 0;
        this._ringUniforms.uPulse.value = 1;
      }
      this.setQuality(this._quality);
      return this;
    }

    update(elapsedSeconds, deltaSeconds, camera) {
      if (this._disposed) return;
      let dt = Number.isFinite(deltaSeconds) ? deltaSeconds :
        (Number.isFinite(elapsedSeconds) && this._lastElapsed !== null ? elapsedSeconds - this._lastElapsed : 0);
      dt = Math.max(0, Math.min(.1, dt));
      if (Number.isFinite(elapsedSeconds)) this._lastElapsed = elapsedSeconds;
      // A local clock keeps reduced motion frozen and resumes without a jump.
      if (!this._reducedMotion) {
        this._clock += dt;
        this._revealAge += dt;
        this._selected += (this._selectedTarget - this._selected) * (1 - Math.exp(-dt * 12));
      }
      const ready = this._reducedMotion ? 1 : smooth(.04, .52, this._revealAge);
      const reveal = this._reducedMotion ? 1.06 : -.06 + smooth(.16, 1.72, this._revealAge) * 1.12;
      const uniforms = this._animatedUniforms;
      for (let i = 0; i < uniforms.length; i++) {
        uniforms[i].uTime.value = this._clock;
        uniforms[i].uSelected.value = this._selected;
        uniforms[i].uReady.value = ready;
      }
      this._ringUniforms.uPulse.value = this._reducedMotion ? 1 : Math.min(1, this._revealAge / 1.1);
      this._characterUniforms.uTime.value = this._clock;
      this._characterUniforms.uReveal.value = reveal;
      this._characterUniforms.uSelected.value = this._selected;
      this._character.position.y = this._reducedMotion ? 0 : Math.sin(this._clock * 1.25) * .012 * smooth(1.5, 2.2, this._revealAge);

      if (camera && camera.isCamera) {
        camera.getWorldPosition(this._cameraPosition);
        this._billboard.getWorldPosition(this._centerPosition);
        const dx = this._cameraPosition.x - this._centerPosition.x;
        const dz = this._cameraPosition.z - this._centerPosition.z;
        if (dx * dx + dz * dz > 1e-10) {
          // Cylindrical billboard: remain upright. Cancel every parent rotation,
          // including an opponent group rotated by PI, using world quaternions.
          this._facingQuaternion.setFromAxisAngle(this._worldUp, Math.atan2(dx, dz));
          this.group.getWorldQuaternion(this._parentQuaternion);
          this._billboard.quaternion.copy(this._parentQuaternion).invert().multiply(this._facingQuaternion);
        }
      }
    }

    dispose() {
      if (this._disposed) return;
      this._disposed = true;
      if (this.group.parent) this.group.parent.remove(this.group);
      this.group.clear();
      this.group.userData.hologramActor = null;
      this._geometries.forEach(geometry => geometry.dispose());
      this._materials.forEach(material => material.dispose());
      this._geometries.clear();
      this._materials.clear();
      // Material.dispose() does not dispose map/uniform textures. They belong to
      // the caller and can be reused by other actors and card previews.
    }
  }

  global.HologramActor = HologramActor;
})(window);
