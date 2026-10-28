import * as THREE from 'three';
import { isMirrored, templateFor } from './dentalModels';

export function instantiateTooth(template: THREE.Group, tooth: number) {
  const model = template.clone(true);
  model.scale.x = isMirrored(tooth) ? -1 : 1;
  const teeth: THREE.Mesh[] = [];
  model.traverse(object => {
    if (!(object instanceof THREE.Mesh)) return;
    object.userData.tooth = tooth;
    object.material = Array.isArray(object.material) ? object.material.map(material => material.clone()) : object.material.clone();
    teeth.push(object);
  });
  return { model, teeth };
}

export function buildDentalArch(row: number[], upper: boolean, offset: number, templates: Map<number, THREE.Group>) {
  const group = new THREE.Group();
  const teeth: THREE.Mesh[] = [];
  const primary = row[0] >= 51;
  const widths = row.map(tooth => templateFor(tooth).width);
  const total = widths.reduce((sum, width) => sum + width, 0);
  const rx = total / (Math.PI * 1.04), rz = rx * 1.06;
  const neckY = (upper ? 1 : -1) * (primary ? 1.28 : 1.7) + offset;
  const gumY = neckY + (upper ? .45 : -.45);
  let distance = 0;
  row.forEach((tooth, index) => {
    const template = templateFor(tooth);
    const theta = -Math.PI / 2 + (distance + widths[index] / 2) / total * Math.PI;
    distance += widths[index];
    const instance = instantiateTooth(templates.get(template.id)!, tooth);
    instance.model.position.set(Math.sin(theta) * rx, neckY, Math.cos(theta) * rz - rz / 2);
    instance.model.rotation.y = theta;
    group.add(instance.model); teeth.push(...instance.teeth);
  });
  const path = new THREE.CatmullRomCurve3(Array.from({ length: 65 }, (_, i) => {
    const theta = -Math.PI / 2 + i / 64 * Math.PI;
    return new THREE.Vector3(Math.sin(theta) * rx, gumY, Math.cos(theta) * rz - rz / 2);
  }));
  const radius = primary ? .45 : .54;
  const gum = new THREE.TubeGeometry(path, 128, radius, 20, false);
  const position = gum.getAttribute('position');
  const gumColors: number[] = [];
  for (let i = 0; i < position.count; i++) {
    const relativeY = position.getY(i) - gumY;
    const t = Math.floor(i / 21) / 128;
    const towardCrown = Math.max(0, (upper ? -1 : 1) * relativeY / radius);
    const scallop = Math.cos(t * row.length * Math.PI * 2) * .08 * towardCrown ** 2;
    position.setY(i, gumY + relativeY + (upper ? 1 : -1) * scallop);
    const shade = .91 + .09 * (1 - towardCrown);
    gumColors.push(shade, shade, shade);
  }
  gum.setAttribute('color', new THREE.Float32BufferAttribute(gumColors, 3)); gum.computeVertexNormals();
  const gumMaterial = new THREE.MeshPhysicalMaterial({ color: 0xca7b82, vertexColors: true, roughness: .6, clearcoat: .1 });
  group.add(new THREE.Mesh(gum, gumMaterial));
  const endMaterial = gumMaterial.clone(); endMaterial.vertexColors = false;
  for (const t of [0, 1]) {
    const end = new THREE.Mesh(new THREE.SphereGeometry(radius, 20, 16), endMaterial);
    end.position.copy(path.getPoint(t)); group.add(end);
  }
  return { group, teeth };
}
