import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { flatten, join, weld, simplify, prune, dedup, clearNodeTransform } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import fs from 'fs';
const [,, inp, out, ratio, texdir] = process.argv;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(inp);
for (const a of doc.getRoot().listAnimations()) a.dispose();
await MeshoptSimplifier.ready;
await doc.transform(dedup(), flatten());
for (const n of doc.getRoot().listNodes()) if (n.getMesh()) clearNodeTransform(n);
{ const scene = doc.getRoot().listScenes()[0]; const holder = doc.createNode('model');
  for (const n of [...scene.listChildren()]) { scene.removeChild(n); holder.addChild(n); }
  scene.addChild(holder); }
const err = Number(process.argv[6] || 0.002);
// Manual merge: one primitive per material (join() skips these files).
{ const root = doc.getRoot(); const groups = new Map();
  for (const m of root.listMeshes()) for (const p of m.listPrimitives()) {
    if (p.getMode() !== 4) continue; const k = p.getMaterial(); if (!groups.has(k)) groups.set(k, []); groups.get(k).push(p); }
  const buf = root.listBuffers()[0]; const mesh = doc.createMesh('merged');
  for (const [mat, prims] of groups) {
    const P = [], Nn = [], U = [], I = []; let base = 0;
    for (const p of prims) { const pos = p.getAttribute('POSITION'), nor = p.getAttribute('NORMAL'), uv = p.getAttribute('TEXCOORD_0'); const c = pos.getCount();
      const t = [0,0,0], t2 = [0,0];
      for (let i = 0; i < c; i++) { P.push(...pos.getElement(i, t)); Nn.push(...(nor ? nor.getElement(i, t) : [0,1,0])); U.push(...(uv ? uv.getElement(i, t2) : [0,0])); }
      const idx = p.getIndices(); if (idx) for (let i = 0; i < idx.getCount(); i++) I.push(idx.getScalar(i) + base); else for (let i = 0; i < c; i++) I.push(i + base);
      base += c; }
    const prim = doc.createPrimitive().setMaterial(mat)
      .setAttribute('POSITION', doc.createAccessor().setType('VEC3').setArray(new Float32Array(P)).setBuffer(buf))
      .setAttribute('NORMAL', doc.createAccessor().setType('VEC3').setArray(new Float32Array(Nn)).setBuffer(buf))
      .setAttribute('TEXCOORD_0', doc.createAccessor().setType('VEC2').setArray(new Float32Array(U)).setBuffer(buf))
      .setIndices(doc.createAccessor().setType('SCALAR').setArray(new Uint32Array(I)).setBuffer(buf));
    mesh.addPrimitive(prim); }
  for (const n of root.listNodes()) n.setMesh(null);
  for (const m of root.listMeshes()) if (m !== mesh) m.dispose();
  const node = doc.createNode('model').setMesh(mesh); root.listScenes()[0].addChild(node); }
await doc.transform(weld(),
  simplify({ simplifier: MeshoptSimplifier, ratio: Number(ratio), error: err }), prune());
let tris = 0, prims = 0;
for (const m of doc.getRoot().listMeshes()) for (const p of m.listPrimitives()) { prims++; tris += (p.getIndices() ? p.getIndices().getCount() : p.getAttribute('POSITION').getCount()) / 3; }
console.log(out, 'tris', tris, 'prims', prims);
if (texdir) { fs.mkdirSync(texdir, { recursive: true }); doc.getRoot().listTextures().forEach((t, i) => fs.writeFileSync(`${texdir}/${i}_${(t.getName()||'t').replace(/[^\w.]/g,'_')}.${t.getMimeType().split('/')[1]}`, t.getImage())); }
await io.write(out, doc);
