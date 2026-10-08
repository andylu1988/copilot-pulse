# Matrix artwork

Saved embedded assets:

- [Mini background — flare](matrix-art-mini.png)
- [Portrait background — sunburst](matrix-art-portrait.png)
- [Wide background — flare](matrix-art-wide.png)
- [Dimensions, deployment names and hashes](matrix-art-provenance.json)

The wide background uses separately resized top, center and bottom regions to keep decorative rails clear of the history window headings. The app loads each embedded bitmap once and renders live labels over it.

Generated with the user-provided Azure deployments gpt-image-2.5-flare and gpt-image-2.5-sunburst using the bundled imagegen CLI. Two high-quality generations. Assets resized locally with Pillow for mini, portrait and wide surfaces. No API credentials are included or required at runtime. Numeric values, labels and animated rain are rendered by WPF, not generated into the images.

## matrix-obsidian-prompt.txt

Use case: stylized-concept
Asset type: production background texture for a tiny desktop financial readout, not a screenshot or a product photograph.
Primary request: a beautifully crafted geek instrument surface, black obsidian and machined graphite with precise emerald optical details. A restrained premium hardware aesthetic, immaculate and credible.
Composition: flat orthographic front view, edge-to-edge rectangular texture. The central horizontal band across the entire middle 65 percent of the canvas is almost plain charcoal black, with very subtle satin microtexture, reserved for large live numbers rendered separately. Concentrate detail at the far left and right edges and in the top and bottom 15 percent: recessed graphite seams, very fine etched circuit paths, a few tiny emerald luminous endpoints, thin chamfered black metal rails. Upper left edge has a soft green grazing reflection, lower right edge a very subtle silver reflection. The central band must remain quiet when cropped to an extremely wide 2.35:1 rectangle.
Color palette: near-black #07100D, graphite #17211C, muted emerald #4BE39B, tiny desaturated silver accents. At least 85 percent dark surfaces. Physically believable fine brushed metal, optical glass sliver details, restrained highlights.
Constraints: NO text, NO letters, NO digits, NO logos, NO pseudo writing, NO currency values, NO charts, NO buttons, NO complete UI, NO external background, NO perspective view, NO large central ornament. Do not bake in matrix rain; it will animate separately in the app. Keep texture detail legible at very small scale. Avoid bright neon fog, oversaturated green, decorative clutter, and large luminous borders.


## matrix-portrait-prompt.txt

Use case: stylized-concept
Asset type: production portrait background for a premium dark desktop instrument panel. This is an edge-to-edge flat background texture, not a mockup.
Primary request: beautifully detailed black machined graphite and smoked glass with emerald micro-circuit accents, professional geek workstation visual design.
Composition: straight-on orthographic vertical 2:3 canvas. Keep the center 80 percent of width exceptionally calm, almost solid black satin graphite, available for overlaid software labels, numbers, and cards. Confine intricate etched circuits, tiny luminous green connection points, precision seams, and beveled metal slivers to the leftmost and rightmost 8 percent and very top/bottom edges. A barely visible diagonal brushed-metal grain and a dark smoked-glass plane may span the middle. The upper 30 percent needs dark negative space for the main readout. Subtle emerald reflected light at the top left edge, desaturated silver reflected light at the bottom right edge. Precise, expensive materials, understated hardware aesthetic.
Color palette: predominantly charcoal and near-black #07100D, graphite #17211C, sparse emerald #4BE39B and muted silver. At least 90 percent dark surfaces. Flat illumination in the central region, restrained grazing highlights at the extreme perimeter.
Constraints: absolutely no text, no numbers, no letters, no logos, no pseudo writing, no buttons, no charts, no rendered user interface, no visible device in a room, no outside background, no perspective view, no dominant central object, no bright neon haze. Dynamic matrix glyphs will be overlaid separately by the application. Must work as a restrained background behind real data, not compete with it.
