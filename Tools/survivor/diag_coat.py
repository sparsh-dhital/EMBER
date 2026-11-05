import bpy, bmesh
o = bpy.data.objects["Survivor_Coat"]
bm = bmesh.new(); bm.from_mesh(o.data)
bnd = [e for e in bm.edges if e.is_boundary]
# group boundary edges into loops
seen, loops = set(), []
for e in bnd:
    if e in seen: continue
    stack, comp = [e], []
    while stack:
        x = stack.pop()
        if x in seen: continue
        seen.add(x); comp.append(x)
        for v in x.verts:
            for y in v.link_edges:
                if y.is_boundary and y not in seen: stack.append(y)
    zs = [ (o.matrix_world @ v.co).z for x in comp for v in x.verts ]
    ys = [ (o.matrix_world @ v.co).y for x in comp for v in x.verts ]
    loops.append((len(comp), round(min(zs),2), round(max(zs),2), round(sum(ys)/len(ys),2)))
loops.sort(key=lambda l: -l[0])
print("COAT verts", len(bm.verts), "faces", len(bm.faces), "boundary loops", len(loops))
for l in loops[:25]: print("LOOP edges=%d z=[%s..%s] meanY=%s" % l)
# normals: fraction of faces whose normal points toward the body centre
