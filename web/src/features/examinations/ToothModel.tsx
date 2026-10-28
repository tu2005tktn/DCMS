import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Space, Spin } from 'antd';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { buildDentalArch, instantiateTooth } from './odontogramGeometry';
import { modelUrl, templateFor, toothName } from './dentalModels';

export type ModelProps = { rows: number[][]; selected: number[]; recorded: number[]; disabled: boolean; onSelect: (tooth: number) => void; onFallback: () => void; labelRequest?: { tooth: number; sequence: number } };

function disposeObjects(roots: THREE.Object3D[]) {
  const geometries = new Set<THREE.BufferGeometry>(), materials = new Set<THREE.Material>(), textures = new Set<THREE.Texture>();
  roots.forEach(root => root.traverse(object => {
    if (!(object instanceof THREE.Mesh)) return;
    geometries.add(object.geometry);
    (Array.isArray(object.material) ? object.material : [object.material]).forEach(material => {
      materials.add(material);
      Object.values(material).forEach(value => { if (value instanceof THREE.Texture) textures.add(value); });
    });
  }));
  textures.forEach(texture => { const data = texture.source.data; if (typeof ImageBitmap !== 'undefined' && data instanceof ImageBitmap) data.close(); texture.dispose(); });
  geometries.forEach(geometry => geometry.dispose()); materials.forEach(material => material.dispose());
}

export default function ToothModel(props: ModelProps) {
  const host = useRef<HTMLDivElement>(null);
  const latest = useRef(props); latest.current = props;
  const reset = useRef<() => void>(() => {}), update = useRef<() => void>(() => {});
  const requestCallout = useRef<(tooth: number) => boolean>(() => false);
  const handledRequest = useRef<number | undefined>(undefined);
  const [callout, setCallout] = useState<{ tooth: number; x: number; y: number; above: boolean }>();
  const inspectView = useRef<(preset: 'occlusal' | 'side') => void>(() => {});
  const [error, setError] = useState(''), [loading, setLoading] = useState(false);
  const [hover, setHover] = useState<number>(), [focus, setFocus] = useState<number>();
  const rowsKey = JSON.stringify(props.rows);
  const displayed = props.rows.flat();
  const focused = focus != null && displayed.includes(focus) ? focus : undefined;
  useEffect(() => { if (focus != null && !displayed.includes(focus)) setFocus(undefined); }, [rowsKey, focus]);
  useEffect(() => {
    const outside = (event: PointerEvent) => {
      if (!host.current?.parentElement?.contains(event.target as Node)) setCallout(undefined);
    };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') setCallout(undefined); };
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, []);
  useEffect(() => {
    const request = props.labelRequest;
    if (!request || loading || handledRequest.current === request.sequence) return;
    if (focused && focused !== request.tooth) { setFocus(undefined); return; }
    if (requestCallout.current(request.tooth)) {
      handledRequest.current = request.sequence;
      host.current?.scrollIntoView({ block: 'center' });
    }
  }, [props.labelRequest, loading, focused]);
  useEffect(() => {
    const container = host.current;
    if (!container) return;
    const abort = new AbortController();
    const sources: THREE.Group[] = [];
    let renderer: THREE.WebGLRenderer | undefined;
    let cleanup = () => {};
    setError(''); setLoading(true); setHover(undefined); setCallout(undefined);
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
      const engine = renderer;
      engine.setPixelRatio(Math.min(window.devicePixelRatio, 2)); engine.setClearColor(0xe9eff4, 1);
      engine.toneMapping = THREE.ACESFilmicToneMapping; engine.toneMappingExposure = 1.05;
      engine.domElement.setAttribute('aria-label', 'Mô hình răng 3D');
      engine.domElement.tabIndex = 0;
      container.appendChild(engine.domElement);
      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(38, 1, .03, 150);
      const controls = new OrbitControls(camera, engine.domElement);
      controls.minDistance = focused ? .7 : 8; controls.maxDistance = focused ? 25 : 45; controls.enablePan = false;
      scene.add(new THREE.HemisphereLight(0xffffff, 0x8292a6, 1.9));
      const light = new THREE.DirectionalLight(0xfff7ea, 3); light.position.set(-4, 7, 10); scene.add(light);
      const fill = new THREE.DirectionalLight(0xe3efff, 1.3); fill.position.set(7, 2, -3); scene.add(fill);
      const teeth: THREE.Mesh[] = [];
      const viewFill = new THREE.DirectionalLight(0xffffff, .8); scene.add(viewFill, viewFill.target);
      const render = () => {
        viewFill.position.copy(camera.position); viewFill.target.position.copy(controls.target);
        viewFill.target.updateMatrixWorld(); engine.render(scene, camera);
      };
      update.current = () => {
        teeth.forEach(mesh => {
          const tooth = mesh.userData.tooth as number;
          (Array.isArray(mesh.material) ? mesh.material : [mesh.material]).forEach(material => {
            if (material instanceof THREE.MeshStandardMaterial) material.color.set(focused ? 0xffffff : latest.current.selected.includes(tooth) ? 0xeaa532 : latest.current.recorded.includes(tooth) ? 0x62bcb1 : 0xffffff);
          });
        }); render();
      };
      let modelBounds: THREE.Box3 | undefined;
      reset.current = () => {
        if (!modelBounds) return;
        const center = modelBounds.getCenter(new THREE.Vector3()), size = modelBounds.getSize(new THREE.Vector3());
        const fov = THREE.MathUtils.degToRad(camera.fov);
        const distance = Math.max(size.y / 2 / Math.tan(fov / 2), size.x / 2 / Math.tan(fov / 2) / camera.aspect) * 1.3 + size.z * .4;
        controls.target.copy(center);
        camera.position.copy(center).add(new THREE.Vector3(focused ? .25 : .3, focused ? .1 : .18, 1).normalize().multiplyScalar(distance));
        controls.update(); render();
      };
      inspectView.current = preset => {
        if (!focused || !modelBounds) return;
        const center = modelBounds.getCenter(new THREE.Vector3()), size = modelBounds.getSize(new THREE.Vector3());
        if (preset === 'occlusal') {
          const upper = [1, 2, 5, 6].includes(Math.floor(focused / 10));
          const sign = upper ? -1 : 1;
          const surfaceY = upper ? modelBounds.min.y : modelBounds.max.y;
          const distance = Math.max(size.x, size.z) / 2 / Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * 1.4;
          controls.target.set(center.x, surfaceY, center.z);
          camera.position.set(center.x, surfaceY + sign * distance, center.z + .02);
        } else {
          const distance = Math.max(size.y, size.z / camera.aspect) / 2 / Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * 1.35;
          controls.target.copy(center); camera.position.copy(center).add(new THREE.Vector3(distance, .08, .01));
        }
        controls.update(); render();
      };
      const resize = new ResizeObserver(() => {
        const width = container.clientWidth, height = container.clientHeight;
        if (!width || !height) return;
        engine.setSize(width, height); camera.aspect = width / height; camera.updateProjectionMatrix();
        reset.current(); render();
      }); resize.observe(container);
      controls.addEventListener('change', render);
      const closeCallout = () => setCallout(undefined);
      controls.addEventListener('start', closeCallout);
      const showCallout = (tooth: number, x: number, y: number) => {
        const width = container.clientWidth, height = container.clientHeight;
        setCallout({ tooth, x: Math.max(8, Math.min(width - Math.min(264, width - 16) - 8, x - 100)), y: y < height / 2 ? y + 14 : y - 14, above: y >= height / 2 });
      };
      requestCallout.current = tooth => {
        const matching = teeth.filter(mesh => mesh.userData.tooth === tooth);
        if (!matching.length) return false;
        const bounds = new THREE.Box3(); matching.forEach(mesh => bounds.expandByObject(mesh));
        const point = bounds.getCenter(new THREE.Vector3()).project(camera);
        if (point.z < -1 || point.z > 1) return false;
        showCallout(tooth, (point.x + 1) / 2 * container.clientWidth, (1 - point.y) / 2 * container.clientHeight);
        return true;
      };
      const raycaster = new THREE.Raycaster(), point = new THREE.Vector2();
      const hit = (event: PointerEvent) => {
        const rect = engine.domElement.getBoundingClientRect();
        point.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
        raycaster.setFromCamera(point, camera);
        return raycaster.intersectObjects(scene.children, true)[0]?.object.userData.tooth as number | undefined;
      };
      let start = { x: 0, y: 0 };
      const down = (event: PointerEvent) => { start = { x: event.clientX, y: event.clientY }; };
      const move = (event: PointerEvent) => { const tooth = hit(event); setHover(tooth); engine.domElement.style.cursor = tooth ? 'pointer' : 'grab'; };
      const up = (event: PointerEvent) => {
        if (Math.hypot(event.clientX - start.x, event.clientY - start.y) > 5) return;
        const tooth = hit(event);
        if (!tooth) { setCallout(undefined); return; }
        const rect = engine.domElement.getBoundingClientRect();
        showCallout(tooth, event.clientX - rect.left, event.clientY - rect.top);
        if (!latest.current.disabled) latest.current.onSelect(tooth);
      };
      const leave = () => setHover(undefined);
      const lost = (event: Event) => { event.preventDefault(); abort.abort(); setLoading(false); setError('Mô hình 3D bị gián đoạn. Tiếp tục ghi khám bằng sơ đồ 2D.'); };
      const canvas = engine.domElement;
      const keyboardTeeth = focused ? [focused] : displayed;
      let keyboardIndex = 0;
      const key = (event: KeyboardEvent) => {
        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) {
          event.preventDefault();
          keyboardIndex = (keyboardIndex + (event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : 1) + keyboardTeeth.length) % keyboardTeeth.length;
          requestCallout.current(keyboardTeeth[keyboardIndex]);
        } else if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault(); const tooth = keyboardTeeth[keyboardIndex];
          requestCallout.current(tooth); if (!latest.current.disabled) latest.current.onSelect(tooth);
        }
      };
      canvas.addEventListener('keydown', key);
      canvas.addEventListener('pointerdown', down); canvas.addEventListener('pointermove', move); canvas.addEventListener('pointerup', up); canvas.addEventListener('pointerleave', leave); canvas.addEventListener('webglcontextlost', lost);
      cleanup = () => {
        resize.disconnect(); controls.removeEventListener('change', render); controls.removeEventListener('start', closeCallout); controls.dispose();
        canvas.removeEventListener('keydown', key);
        canvas.removeEventListener('pointerdown', down); canvas.removeEventListener('pointermove', move); canvas.removeEventListener('pointerup', up); canvas.removeEventListener('pointerleave', leave); canvas.removeEventListener('webglcontextlost', lost);
        disposeObjects([scene, ...sources]); engine.dispose(); engine.forceContextLoss(); canvas.remove();
      };
      const loader = new GLTFLoader();
      const ids = [...new Set((focused ? [focused] : displayed).map(tooth => templateFor(tooth).id))];
      void Promise.all(ids.map(async id => {
        const response = await fetch(modelUrl(id, focused != null), { signal: abort.signal });
        if (!response.ok) throw new Error('MODEL_HTTP_' + response.status);
        const buffer = await response.arrayBuffer(); if (abort.signal.aborted) throw new DOMException('Aborted', 'AbortError');
        const gltf = await loader.parseAsync(buffer, '');
        if (abort.signal.aborted) { disposeObjects([gltf.scene]); throw new DOMException('Aborted', 'AbortError'); }
        sources.push(gltf.scene); return [id, gltf.scene] as const;
      })).then(entries => {
        if (abort.signal.aborted) return;
        const templates = new Map(entries);
        if (focused) {
          const instance = instantiateTooth(templates.get(templateFor(focused).id)!, focused);
          scene.add(instance.model); teeth.push(...instance.teeth);
        } else props.rows.forEach((row, index) => {
          const arch = buildDentalArch(row, index % 2 === 0, Math.floor(index / 2) * -7, templates);
          scene.add(arch.group); teeth.push(...arch.teeth);
        });
        scene.updateMatrixWorld(true); modelBounds = new THREE.Box3();
        scene.children.filter(object => !(object instanceof THREE.Light)).forEach(object => modelBounds!.expandByObject(object));
        reset.current(); update.current(); setLoading(false);
      }).catch(() => {
        if (abort.signal.aborted) return;
        abort.abort(); setLoading(false); setError('Không tải được mô hình răng. Bạn có thể tiếp tục bằng sơ đồ 2D.');
      });
    } catch {
      abort.abort(); renderer?.dispose(); renderer?.domElement.remove(); setLoading(false);
      setError('Thiết bị chưa hỗ trợ WebGL. Tiếp tục ghi khám bằng sơ đồ 2D.');
    }
    return () => { abort.abort(); cleanup(); reset.current = () => {}; update.current = () => {}; inspectView.current = () => {}; requestCallout.current = () => false; };
  }, [rowsKey, focused]);
  useEffect(() => update.current(), [props.selected, props.recorded]);
  return <div className="tooth-model-wrap">
    <p>Mô hình minh họa · Kéo để xoay, cuộn để phóng to · Chạm răng để chọn / bỏ chọn</p>
    {focused && <Space wrap className="tooth-inspector-controls">
      <Button disabled={false} onClick={() => { setCallout(undefined); setFocus(undefined); }}>Xem toàn hàm</Button>
      {focused && <span>Răng {focused} · Đang xem riêng thân và chân răng</span>}
      {focused && <Space wrap><Button disabled={false} onClick={() => reset.current()}>Chính diện</Button><Button disabled={false} onClick={() => inspectView.current('occlusal')}>Mặt nhai / cắn</Button><Button disabled={false} onClick={() => inspectView.current('side')}>Mặt bên</Button></Space>}
    </Space>}
    {error && <Alert type="warning" message={error} action={<Button disabled={false} onClick={props.onFallback}>Về sơ đồ 2D</Button>} />}
    <div className="tooth-model-stage" style={error ? { display: 'none' } : undefined}>
      <div ref={host} className="tooth-model" aria-busy={loading} />
      {callout && !loading && !error && <div role="group" aria-label={`Tùy chọn răng ${callout.tooth}`} className="tooth-context-label" style={{ left: callout.x, top: callout.y, transform: callout.above ? 'translateY(-100%)' : undefined }}>
        <button type="button" className="tooth-label-close" aria-label="Đóng nhãn răng" onClick={() => setCallout(undefined)}>×</button>
        <strong>Răng {callout.tooth}</strong><span>{toothName(callout.tooth)}</span>
        <Button type="primary" size="small" disabled={false} aria-label={`Xem riêng răng ${callout.tooth}`} onClick={() => { setFocus(callout.tooth); setCallout(undefined); }}>Xem riêng</Button>
      </div>}
      {loading && <div className="tooth-model-loading" role="status"><Spin /><span>Đang tải mô hình răng…</span></div>}
    </div>
    {!error && <div className="model-controls"><span role="status">{hover ? `Răng ${hover}` : 'Di chuyển lên răng để xem số FDI'}</span><Button disabled={false} onClick={() => reset.current()}>Đặt lại góc nhìn</Button></div>}
    <small className="dental-model-credit">Mẫu răng: <a href="https://sketchfab.com/DundeeDental" target="_blank" rel="noreferrer">University of Dundee, School of Dentistry</a> · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noreferrer">CC BY 4.0</a> · Đã chuẩn hóa, tối ưu và phản chiếu bên đối diện. <a href={`${import.meta.env.BASE_URL}models/dental/ATTRIBUTION.md`} target="_blank" rel="noreferrer">Nguồn từng mẫu</a></small>
  </div>;
}
