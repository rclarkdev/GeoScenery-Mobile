import { Scene } from '../scene.model';

/**
 * Builds the DOM content for a scene's Leaflet marker popup.
 *
 * Scene-provided values (such as the title) are assigned via `textContent`
 * and never interpolated into an HTML string, so attacker-controlled text can
 * never be interpreted as markup or script by Leaflet's popup renderer.
 */
export function createScenePopupContent(scene: Scene): HTMLDivElement {
  const container = document.createElement('div');
  const title = document.createElement('strong');

  title.textContent = scene.title;
  container.appendChild(title);

  return container;
}