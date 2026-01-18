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

  void main() {
    vec2 resolution = u_res;
    vec2 uv = gl_FragCoord.xy / resolution.xy;

    float aspect = resolution.x / resolution.y;
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);

    float t = u_time * 0.5;

    vec3 baseBottom = vec3(0.01, 0.01, 0.03);
    vec3 baseTop    = vec3(0.03, 0.06, 0.14);
    float gy = smoothstep(-0.3, 1.1, uv.y + uv.x * 0.1);
    vec3 col = mix(baseBottom, baseTop, gy);

    vec2 c1 = vec2(0.30, 0.45) + 0.10 * vec2(sin(t * 0.7), cos(t * 0.6));
    vec2 c2 = vec2(0.70, 0.65) + 0.12 * vec2(cos(t * 0.4), sin(t * 0.5));
    vec2 c3 = vec2(0.50, 0.30) + 0.15 * vec2(sin(t * 0.3), sin(t * 0.9));

    vec2 cp1 = (c1 - 0.5) * vec2(aspect, 1.0);
    vec2 cp2 = (c2 - 0.5) * vec2(aspect, 1.0);
    vec2 cp3 = (c3 - 0.5) * vec2(aspect, 1.0);

    float d1 = length(p - cp1);
    float d2 = length(p - cp2);
    float d3 = length(p - cp3);

    float glow1 = exp(-3.5 * d1 * d1);
    float glow2 = exp(-3.0 * d2 * d2);
    float glow3 = exp(-4.0 * d3 * d3);

    vec3 col1 = vec3(0.20, 0.55, 1.00);
    vec3 col2 = vec3(0.95, 0.40, 0.90);
    vec3 col3 = vec3(0.25, 0.95, 0.75);

    col += col1 * glow1;
    col += col2 * glow2;
    col += col3 * glow3;

    col = pow(col, vec3(1.0 / 1.1));
    col = clamp(col, 0.0, 1.0);

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