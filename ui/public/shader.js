function startShaderBackground(canvasId) {
  const canvas = document.getElementById(canvasId);
  const gl = canvas.getContext('webgl', { antialias: false, preserveDrawingBuffer: false });
  if (!gl) {
    console.warn('WebGL nicht verfügbar...');
    return;
  }

  function resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const width = Math.floor(gl.canvas.clientWidth * dpr);
    const height = Math.floor(gl.canvas.clientHeight * dpr);
    if (gl.canvas.width !== width || gl.canvas.height !== height) {
      gl.canvas.width = width;
      gl.canvas.height = height;
      gl.viewport(0, 0, width, height);
    }
  }
  const styleResize = () => {
    canvas.style.width = '100vw';
    canvas.style.height = '100vh';
    resize();
  };
  window.addEventListener('resize', styleResize);
  styleResize();

  const vertSrc = `
    attribute vec2 a_pos;
    void main() {
      gl_Position = vec4(a_pos, 0.0, 1.0);
    }
  `;

const fragSrc = `
  precision mediump float;

  uniform float u_time;
  uniform vec2 u_res;

  const int NUM_EXPLOSIONS = 4;
  const int NUM_PARTICLES  = 100;

  vec2 Hash12(float t) {
    float x = fract(sin(t * 456.51) * 195.23);
    float y = fract(sin((t + x) * 951.2) * 462.1);
    return vec2(x, y);
  }

  vec2 Hash12_Polar(float t) {
    float a = fract(sin(t * 456.51) * 195.23) * 6.28318530718;
    float r = fract(sin((t + a) * 951.2) * 462.1);
    return vec2(sin(a), cos(a)) * r;
  }
    
  vec3 hsv2rgb(vec3 c) {
    vec3 p = abs(fract(c.x + vec3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0);
    vec3 rgb = clamp(p - 1.0, 0.0, 1.0);
    return c.z * mix(vec3(1.0), rgb, c.y);
  }

  float Explosion(vec2 uv, float t) {
    float sparks = 0.0;
    for (int i = 0; i < NUM_PARTICLES; i++) {
      float fi = float(i);
      vec2 dir = Hash12_Polar(fi + 1.0) * 0.5;
      float d = length(uv - dir * t);

      float brightness = mix(0.0003, 0.001, smoothstep(0.05, 0.0, t));
      brightness *= sin(t * 20.0 + fi) * 0.5 + 0.5;
      brightness *= smoothstep(1.0, 0.7, t);
      sparks += brightness / d;
    }
    return sparks;
  }

  void main() {
    vec2 resolution = u_res;
    float time = u_time * 0.05;

    vec2 uv = (gl_FragCoord.xy - 0.5 * resolution.xy) / resolution.y;
    vec3 col = vec3(0.0);

    for (int i = 0; i < NUM_EXPLOSIONS; i++) {
      float fi = float(i);
      float t  = (time / 1.5) + (fi * 123.4) / float(NUM_EXPLOSIONS);
      float ft = floor(t);

      float rnd = Hash12(fi + 17.123 + ft * float(NUM_EXPLOSIONS)).x;
      float hue = mix(0.64, 0.83, rnd);
      float sat = 0.3;
      float val = 0.5;
      vec3 colour = hsv2rgb(vec3(hue, sat, val));

      vec2 offset = Hash12(fi + 1.0 + ft * float(NUM_EXPLOSIONS)) - 0.5;
      offset *= vec2(0.9 * 1.777, 0.9);
      col += Explosion(uv - offset, fract(t)) * colour;
    }

    col *= 2.0;
    gl_FragColor = vec4(col, 1.0);
  }
`;

  function compile(type, src) {
    const sh = gl.createShader(type);
    gl.shaderSource(sh, src);
    gl.compileShader(sh);
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) {
      throw new Error(gl.getShaderInfoLog(sh));
    }
    return sh;
  }
  function program(vs, fs) {
    const p = gl.createProgram();
    gl.attachShader(p, vs);
    gl.attachShader(p, fs);
    gl.linkProgram(p);
    if (!gl.getProgramParameter(p, gl.LINK_STATUS)) {
      throw new Error(gl.getProgramInfoLog(p));
    }
    return p;
  }

  const vs = compile(gl.VERTEX_SHADER, vertSrc);
  const fs = compile(gl.FRAGMENT_SHADER, fragSrc);
  const prog = program(vs, fs);
  gl.useProgram(prog);

  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
    -1, -1,  1, -1, -1,  1,
    -1,  1,  1, -1,  1,  1
  ]), gl.STATIC_DRAW);

  const locPos = gl.getAttribLocation(prog, 'a_pos');
  gl.enableVertexAttribArray(locPos);
  gl.vertexAttribPointer(locPos, 2, gl.FLOAT, false, 0, 0);

  const uRes = gl.getUniformLocation(prog, 'u_res');
  const uTime = gl.getUniformLocation(prog, 'u_time');

  let start = performance.now();
  function frame() {
    const clientWidth = document.documentElement.clientWidth || window.innerWidth;
    const clientHeight = document.documentElement.clientHeight || window.innerHeight;
    canvas.style.width = clientWidth + 'px';
    canvas.style.height = clientHeight + 'px';
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const width = Math.floor(clientWidth * dpr);
    const height = Math.floor(clientHeight * dpr);
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
      gl.viewport(0, 0, width, height);
    }

    const t = (performance.now() - start) / 1000.0;
    gl.uniform2f(uRes, gl.canvas.width, gl.canvas.height);
    gl.uniform1f(uTime, t);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
    requestAnimationFrame(frame);
  }
  frame();
}

document.addEventListener('DOMContentLoaded', () => {
  requestAnimationFrame(() => startShaderBackground('bg'));
});