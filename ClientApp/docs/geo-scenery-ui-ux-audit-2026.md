# GeoScenery UI/UX Audit and Design-System Proposal (2026)

**Status:** Audit and proposal only. This document does not authorize or describe a broad UI rewrite.  
**Scope:** Source-based review of the Angular/Ionic/Capacitor client and relevant API/data contracts.  
**Product direction:** Cinematic visual exploration, with scenery as the primary product asset.

## Executive summary

GeoScenery already has the foundations of a location-based community product: scene photographs, coordinates, map search, tags, distance filtering, public/private visibility, ratings, visit history, profiles, follows, messaging, and moderation. The main product opportunity is to make imagery the first way people explore while preserving the map and existing workflows.

Today, the authenticated app is primarily a map-first Ionic application. Scene lists use conventional rows, detail pages place a cropped image above a long information/action flow, and key experiences do not share a defined media presentation system. The visual language also varies between the global Ionic theme, map overlays, scene/profile pages, and the editorial About page.

Recommended direction: **Quiet Expedition**—calm, dark-first, editorial, and geographically grounded. Let real scenery provide most of the color; use restrained mineral accents, readable location metadata, and contextual controls. Improve the existing experience in measured stages. Keep Angular, Ionic, Capacitor, Leaflet, current API behavior, and existing tests.

This review is based on source and contracts, not a running application. It does not establish actual contrast ratios, network/bundle performance, production CDN behavior, or behavior on Android/iOS devices.

## 1. Current product and UI assessment

### Application shell and navigation

- The root redirects to authenticated `/scenery`; top-level routes lazy-load authentication, informational/legal, scenery, and visits areas. Lazy feature modules are preloaded after startup.
- The shell uses an Ionic router outlet. The scenery area has bottom tabs for **Search, My Scenes, Messages, and Profile**, with Admin available for administrators.
- Visits has a route but is not a primary tab. There is no distinct onboarding or settings experience, and no in-app notifications surface was identified. Messages expose unread counts.
- There is one shared Angular/Ionic app for web, Android, and iOS; no dedicated desktop shell was identified.
- Preserve the familiar mobile tab pattern initially. Reconsider labels and placement only in a focused navigation stage, after validating task frequency and route behavior.

### Discovery, scenes, and geographic context

- Search is a Leaflet map using OpenStreetMap tiles. It can use saved user coordinates, supports tags, coordinates, radius, and current location, and places markers for scenes with coordinates.
- Map popup content is text/action oriented (title, rating/distance, details), rather than image-first. Keyboard handling for markers/popups is a positive existing accessibility pattern.
- The API supports scene reads/search, public/private visibility, tags, coordinates, ratings, visits, profiles, follows/blocks, messaging, reports, and administration.
- Scene detail includes an image, owner, description, tags, community ratings, coordinates, and rating/visit/report actions. There is no map preview/“nearby scenes” transition identified on detail.
- Scene imagery is currently a single `ImageUrl` in the scene contract. No favorite, collection, category, video, animation, focal-point, or media-provenance fields were found.
- My Scenes and public scene lists use Ionic list rows with thumbnails. The scene detail image uses a 16:9 `object-fit: cover` frame, which can crop off-centre subjects.
- Profiles include public scenes and follower/following features. Messaging, visits, and admin/moderation are existing capabilities and should not be visually removed in a redesign.

### Other surfaces

- Authentication includes sign-in/registration, verification, and reset flows. Informational routes include About, legal documents, and support.
- Scene and profile editing use forms and image uploads. The reusable crop workflow was recently added: browser file selection and native Camera selection both feed a shared crop editor; the cropped file is uploaded on save.
- Admin provides report review, user/scene management, and audit history.
- No favorites, collections, curated categories, seasonal/trending feed, or personalized discovery feature was evidenced. These should not be represented as existing product features in a visual refresh.

### Representative source references

- [Top-level routing](../src/app/app-routing.module.ts)
- [Scenery routing](../src/app/scenery/scenery-routing.module.ts)
- [Scenery tabs](../src/app/scenery/scenery.page.html)
- [Search page](../src/app/scenery/search/search.page.ts)
- [Map popup builder](../src/app/scenery/search/scene-popup.ts)
- [Scene detail template](../src/app/scenery/search/scene-detail/scene-detail.page.html)
- [Scene detail styles](../src/app/scenery/_scene-detail.scss)
- [Global theme variables](../src/theme/variables.scss)
- [About page styles](../src/app/about/about.page.scss)
- [Scene API endpoints](../../GeoScenery.Api/Endpoints/GeoSceneryEndpoints.cs)
- [Scene view models](../../GeoScenery.Api/ViewModels/GeoSceneryViewModels.cs)
- [Scene data model](../../GeoScenery.Data/Models/Scene.cs)

## 2. UX issues and visual inconsistencies

1. **Scenery is subordinate to the map and surrounding chrome.** The default discovery surface is a map, while the personal scene list is a conventional list. The map remains valuable, but should be a mode/context for exploration rather than the only way to begin it.
2. **The map and image experiences are disconnected.** Popups do not show a scene image, detail does not provide a map preview or nearby path, and the user cannot naturally move between “what is this place?” and “what else is nearby?”
3. **Scene image presentation is inconsistent.** Detail uses a wide crop; lists use small thumbnails; no shared viewer/card/metadata behavior or focal point is defined.
4. **The visual foundation is fragmented.** Global Ionic colors, white map overlays, narrow scene/profile content, and the About page’s warmer editorial styling do not form one semantic system.
5. **Responsive behavior is uneven.** About is an editorial responsive page; detail and profile are narrow single-column surfaces; other pages mostly use Ionic mobile layouts expanded to available web width.
6. **Visit history is underexposed.** It is a real feature linked from scene actions but not represented in primary tabs.
7. **Search status feedback deserves review.** Search load failures are recorded in the filter panel; initial loading/empty/error feedback should be visible in the discovery context and tested at narrow sizes.
8. **The available API does not support some desired visual concepts.** Favorites, collections, curated/trending rails, animated media, AI provenance, and alternate image renditions require explicit product/API/storage support before the client can show them truthfully.

## 3. Accessibility findings

### Positive existing patterns

- Ionic labels, roles/status announcements, visible focus patterns in places, and keyboard handling for map markers/popups were identified.
- These are useful foundations to preserve during component extraction and visual changes.

### Confirmed issues and follow-up

- The viewport disables user zoom (`maximum-scale=1`, `user-scalable=no`) in [index.html](../src/index.html). Remove that restriction and verify layout at browser zoom.
- Scene image templates were found without meaningful alternative text. Add context-sensitive alt text for informative images and empty alt text for purely decorative imagery.
- Reduced-motion support appears localized rather than governed by a shared system. Define a global preference and test all newly added transitions.
- Contrast has not been measured. Do not claim WCAG conformance based on palette appearance; measure body text, metadata, map controls, focus indicators, disabled states, and overlays against actual imagery.
- Audit all dialogs, menus, map controls, icon-only buttons, form errors, and touch targets for accessible names, keyboard operation, focus order, and screen-reader feedback.

### Accessibility acceptance checks for each stage

- User zoom remains available through at least 200% without loss of primary content or actions.
- Keyboard users can navigate, activate controls, dismiss overlays, and see focus.
- Informative scenery has useful alternative text; decorative layers are ignored by assistive technology.
- Controls and text meet WCAG AA contrast requirements in default, hover/focus, disabled, and image-overlay contexts.
- Reduced-motion preference suppresses nonessential movement and video autoplay.

## 4. Mobile and native assessment

- The client uses Ionic and Capacitor, including native Camera selection and safe-area-aware styles in some surfaces.
- Scene detail and profile are narrow; the map expands to the available viewport; there is not a unified edge-to-edge scene-viewing mode.
- Safe-area support is not yet a system-wide layout rule. Establish shared top/bottom inset handling before making controls overlay full-bleed imagery.
- Touch behavior, landscape layouts, Android WebView behavior, iOS keyboard behavior, camera permissions, and device memory have not been verified as part of this static audit.
- Retain native Camera’s camera/gallery prompt and existing upload behavior while tuning the experience. Test both platforms on devices or emulators at each relevant stage.

## 5. Web assessment

- Web currently shares the Ionic routes and shell; desktop has no distinct navigation or multi-column discovery layout.
- Some informational content is responsive, but discovery, scene lists, and scene detail largely read as narrow/mobile layouts or expanded map content.
- Desktop should become intentionally editorial: larger imagery, multi-column discovery, keyboard-operable map/list switching, and compact top/side navigation. Do not merely enlarge mobile controls.
- Preserve URL/routing behavior, browser history, keyboard navigation, and responsive interaction parity with the mobile app.

## 6. Performance and media architecture

### Current evidence

- Image uploads accept JPEG, PNG, and WebP, enforce a 5 MiB upload limit, and are resized server-side to configured maximum dimensions of 1600px for scenes and 512px for profiles.
- The client uses `ion-img` in templates. The scene contract exposes one image URL; no thumbnail/medium/original rendition contract or `srcset` strategy was found.
- Storage defaults to local filesystem paths. Production guidance requires a persistent path or an alternate storage implementation.
- The crop flow supports common image formats and keeps the cropped file local until save; this improves upload intent but does not constitute a delivery/CDN strategy.

### Recommended progression

1. **Now:** Avoid eager loading off-screen scene media; use lazy loading, stable aspect-ratio placeholders, explicit load/error states, and correct alternative text. Profile and scene sizes should remain bounded as they are today.
2. **Next:** Add measured responsive rendition delivery (for example thumbnail, medium, full) and use the appropriate size in lists, map sheets, and detail. Preserve the current original-image contract during migration where required.
3. **At scale:** Evaluate object storage/CDN, immutable cache headers, upload format policy, request/cache observability, and retention. Select based on production traffic and hosting constraints rather than assumption.
4. **Validation:** Measure bundle size, image bytes, request counts, decode time, scroll smoothness, and memory on representative mobile devices. This audit did not collect those metrics.

## 7. Proposed GeoScenery visual identity

### Direction: Quiet Expedition

The feeling is a calm invitation to explore—premium but not precious, cinematic but not theatrical, geographically grounded rather than generic travel marketing. Real scenery supplies the dominant color. Typography and location metadata identify place; interface chrome stays restrained and legible.

### Color tokens

Starting values to prototype, **not yet contrast-validated**:

| Token | Suggested value | Role |
|---|---:|---|
| `color-background` | `#101614` | Main dark canvas |
| `color-surface` | `#19211D` | Persistent panels and navigation |
| `color-surface-elevated` | `#222C26` | Menus and raised controls |
| `color-surface-overlay` | `rgba(16, 22, 20, 0.82)` | Contextual surfaces over imagery only |
| `color-text-primary` | `#F3F4ED` | Main text |
| `color-text-secondary` | `#BEC8BF` | Supporting information |
| `color-text-muted` | `#97A59B` | Low-priority metadata (validate contrast) |
| `color-border` | `#354139` | Dividers and control edges |
| `color-accent` | `#D8A85B` | Selected actions and geographic highlights |
| `color-success` | Semantic green | Positive confirmation |
| `color-warning` | Semantic amber | Caution |
| `color-danger` | Semantic red | Errors/destructive actions |

Keep semantic feedback colors distinct from the warm accent. Use dark text on accent-filled controls when contrast testing confirms it. Do not apply overlays to every surface or rely on color alone to convey state.

### Typography

- Use a legible system sans stack for interface text, forms, controls, and metadata.
- Evaluate one restrained editorial serif for large place/scene titles only; avoid loading an external font by default. If chosen, self-host and measure its impact.
- Use consistent heading levels and tabular numerals for coordinates/distance where useful.
- Establish a small role-based scale (display, title, section, body, label, metadata) rather than page-specific arbitrary sizing.

### Spacing, surfaces, and responsive rules

- Initial spacing scale: 4, 8, 12, 16, 24, 32, 48 CSS pixels; tune after implementation review.
- Define semantic surface levels, border/focus rules, and a restrained radius scale. Reserve translucency for controls that sit over imagery.
- Use content-width and breakpoint tokens rather than per-page caps. Validate breakpoints against actual content, not device names.
- Centralize tokens in Sass files imported by the existing Angular styles. Do not add a CSS framework.

## 8. Core scene presentation specifications

### SceneViewer / SceneHero

- Phone portrait: edge-to-edge image where appropriate, with safe-area-aware navigation and a gradient or solid readable title region.
- Landscape and desktop: offer an immersive, intentional frame rather than stretching a phone layout; expose image controls without obscuring the scene.
- Use `cover` for previews/cards only. For full viewing, provide a mode that avoids losing important scenery; if a crop is unavoidable, support an optional focal point as a future contract.
- Include stable loading placeholders, image failure fallback, owner/location context, and accessible actions.
- Keep text and controls understandable if the image is absent or slow.

### SceneCard / SceneGrid

- Image-first card with title and geographic label anchored over a measured gradient; favorite affordance is omitted until the product supports favorites.
- Provide visible focus, clear action names, adequate touch targets, and a desktop hover state that does not hide information from touch/keyboard users.
- Use portrait/landscape variants only where their content context supports them; do not force all scenery into a single crop.
- On phones use efficient single/dual-column layouts based on actual image shapes and text; on desktop use a considered multi-column composition rather than endless uniform cards.

### SceneMetadata / SceneLocation / SceneActions

- Reuse existing owner, tags, coordinates, ratings, and visit/rating/report actions.
- Show location in a human-readable way when available; do not fabricate a place name from raw coordinates.
- Keep ownership and community actions contextually grouped and accessible.

### MediaLoader

- Reserve layout space before loading, expose informative loading/error states, and crossfade the loaded rendition only when it improves continuity.
- Select rendition appropriate to card, map preview, or detail view when the API supports them.
- Do not request full-size assets for off-screen cards.

## 9. Discovery experience proposal

Make **Explore** an imagery-led entry point with an explicit way to switch to **Map**. Begin with the currently supported scene data and search capabilities:

- Image-first scenes from existing search results, with tags/distance where available.
- A clear map/list mode control and retained tag, coordinate, radius, and current-location filters.
- Empty, loading, permission-denied, offline, and error states that appear in the content context.
- Map marker selection opens an image-first sheet or panel using the selected scene data; the action continues to the existing detail route.

Do not imply editorial curation, seasonal rankings, trending or personalized content until there is data and a product decision to support it. Horizontal rails may be introduced only when they represent actual meaningful groups.

## 10. Navigation proposal

- **Mobile:** preserve a compact bottom navigation, with a possible feature-grounded set of Explore, My Scenes, Visits, Messages, and Profile. Consider exposing Visits because it exists today. Keep Admin contextual and role-gated rather than a core discovery destination.
- **Web:** use a compact top/side navigation and allow Explore to use the available width for imagery and map/list context.
- Do not add a Favorites destination until favorite functionality exists.
- Navigation labels, active states, unread badges, route guards, and back behavior must be tested before changing structure.

## 11. Map and scenery relationship

Keep Leaflet as geographic context, not the entire product. Let the user move naturally among:

**scene image → location/context → map → nearby search → another scene**

Incrementally connect what exists:

1. Add imagery and location context to a selected-marker preview.
2. Add “View on map” from scene detail, using its existing coordinates.
3. Offer nearby search by applying existing coordinate/radius search capabilities.
4. Provide a clear return path from the map to the selected scene.

Do not claim that distance filtering is a personalized “nearby scenes” feed unless its behavior and data support that promise. Map controls and marker selection must remain keyboard and screen-reader accessible.

## 12. Motion system

Use a shared, restrained motion vocabulary:

- Image changes: short opacity crossfades.
- Scene/detail transitions: subtle opacity and small positional change; avoid large zoom that causes disorientation.
- Controls/favorite action: quick color/opacity feedback. Favorite interaction remains future functionality.
- Sheets/dialogs: predictable enter/exit with focus management; no bounce.
- Map changes: preserve spatial context and avoid gratuitous camera movement.
- Loading: static placeholder first; any shimmer must be low contrast and disabled for reduced motion.

Centralize duration/easing tokens. Support `prefers-reduced-motion` globally, with essential state changes communicated without animation.

## 13. Animated scenery and AI-generated media

Neither video/animated scene playback nor AI-generated media provenance is present in the inspected scene contract. Treat both as later product/API work, not as a client-only visual embellishment.

An eventual media contract should preserve a still image and could add optional fields such as:

```json
{
  "imageUrl": "...",
  "loopVideoUrl": "...",
  "posterUrl": "...",
  "loopDurationSeconds": 12,
  "mediaOrigin": "photograph"
}
```

Finalize names, validation, permissions, transcoding, storage, and provenance semantics with API/product owners before implementing. For any loop:

- Keep the still image as the reliable fallback and default.
- Use muted inline playback; request no eager download before the media is near/visible.
- Pause when off-screen; respect reduced motion, data/battery constraints, and WebView autoplay rules.
- Define codecs, adaptive sizes, poster, loop quality, cache, and moderation policy before release.
- Clearly label AI-generated/AI-enhanced material and never imply an invented location is a real photograph.

AI-assisted uses (conceptual promotional imagery, seasonal visualizations, or onboarding) require explicit provenance and consent/policy decisions. Never silently replace documentary scene photography with generated media.

## 14. Component and style architecture proposal

Keep the current Angular/Ionic architecture. Extract components only when they provide reuse or a coherent behavior boundary.

**Candidate shared components, staged:**

- `SceneViewer` / `SceneHero`: detail/immersive display and image states.
- `SceneCard` and `SceneGrid`: consistent image-first discovery and lists.
- `SceneMetadata`, `SceneLocation`, `SceneActions`: reuse only where the same information/action composition repeats.
- `MapScenePanel`: selected-scene map preview linked to existing routes/data.
- `MediaLoader`: progressive image state and fallback.
- `ImmersiveHeader` / `ImmersiveBottomNav`: only after the shell/navigation stage proves a shared need.

**Candidate Sass organization:**

```text
ClientApp/src/
  styles/
    _tokens.scss
    _colors.scss
    _typography.scss
    _spacing.scss
    _motion.scss
    _surfaces.scss
    _responsive.scss
    _media.scss
```

These are proposals, not files to create en masse. Begin with semantic tokens in the existing global theme structure, then split only when it improves maintainability. Avoid duplicated page-level variables and do not introduce a new styling framework.

## 15. Implementation roadmap and priorities

### P0 — Release/blocking: accessibility and reliable states

- Restore browser zoom.
- Add useful image alternatives and ensure decorative images are ignored.
- Make search loading, empty, permission, and error states visible in context.
- Test keyboard/focus/screen-reader basics and contrast for high-use flows.
- Preserve functionality and tests; fix only issues confirmed by implementation/testing.

### P1 — Major product improvement: visual foundations and discovery

1. Introduce tested semantic color/type/spacing/surface tokens.
2. Establish shared safe-area, content-width, and responsive shell rules.
3. Build the reusable scene media/card/metadata primitives, backed by existing contracts.
4. Create image-led Explore with a clear Map mode while preserving current filters.
5. Improve map marker preview and scene-to-map/location continuity.
6. Review whether Visits belongs in primary navigation based on user tasks.

### P2 — Polish and measured optimization

- Add focal-point-aware display when supported by the data model.
- Refine scene transitions, control feedback, hover/focus, and reduced-motion behavior.
- Measure image delivery, bundle/network cost, responsiveness, and memory on real targets.
- Improve rendition/caching pipeline based on evidence.

### P3 — Future capabilities needing product/API decisions

- Favorites and collections.
- Editorial, seasonal, trending, or personalized discovery.
- Richer location names/nearby suggestions if data supports them.
- Responsive image rendition/CDN contract and storage scaling.
- Optional loop video/animated scenery.
- AI-media provenance and user-facing labeling.
- Social extensions beyond existing follows and messaging.

## 16. Testing and acceptance plan

After each implementation stage:

- Run the relevant existing Angular tests and build. Do not remove tests to accommodate visual changes.
- Add behavior tests for media load/error fallback, navigation, map selection/detail continuity, reduced motion, and accessibility-critical labels/focus.
- Test narrow portrait, landscape, tablet, and desktop widths; use visual review rather than assuming a CSS breakpoint implies quality.
- Verify keyboard-only navigation, browser zoom, screen-reader names, focus restoration for overlays, and measured contrast.
- Measure media requests, selected image sizes, bundle changes, decode/render behavior, and scrolling on representative mobile hardware.
- Test Android/iOS safe areas, WebView image/video policy, keyboard, native camera/gallery selection, and permission failures on device or emulator.
- For future video, verify still fallback, muted inline behavior, visibility pause, reduced motion, offline/slow network, and battery-conscious loading.

## 17. Audit limitations

This is a static source and contract review. No running client/API, live scene content, real network traces, rendered contrast measurements, automated accessibility scan, Android/iOS device, or app-store build was inspected. Production CDN/cache behavior cannot be inferred from local storage code alone. Recommendations requiring new scene metadata, curated content, favorites, video, AI provenance, or storage transformations need product/API validation before implementation.
