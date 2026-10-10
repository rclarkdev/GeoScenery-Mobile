import { Scene } from '../scene.model';
import { ImageUrlPipe } from '../../shared/image-url.pipe';

/**
 * Builds the DOM content for a scene's Leaflet marker popup.
 *
 * Scene-provided values (such as the title) are assigned via `textContent`
 * and never interpolated into an HTML string, so attacker-controlled text can
 * never be interpreted as markup or script by Leaflet's popup renderer.
 */
export function createScenePopupContent(scene: Scene, onViewDetails: () => void): HTMLDivElement {
  const container = document.createElement('div');
  container.className = 'scene-preview';
  const imageUrl = new ImageUrlPipe().transform(scene.imageUrl);
  if (imageUrl) {
    const image = document.createElement('img');
    image.className = 'scene-preview__image';
    image.src = imageUrl;
    image.alt = `${scene.title} scenery`;
    image.loading = 'lazy';
    image.decoding = 'async';
    image.addEventListener('error', () => image.remove());
    container.append(image);
  }
  const title = document.createElement('strong');
  const metadata = document.createElement('span');
  const detailsButton = document.createElement('button');

  title.textContent = scene.title;
  metadata.textContent = scene.distanceKm == null
    ? `Rating ${scene.rating} / 10`
    : `Rating ${scene.rating} / 10 · ${scene.distanceKm.toFixed(1)} km away`;
  detailsButton.type = 'button';
  detailsButton.className = 'scene-preview__action';
  detailsButton.textContent = 'View details';
  detailsButton.setAttribute('aria-label', `View details for ${scene.title}`);
  detailsButton.addEventListener('click', event => {
    event.stopPropagation();
    onViewDetails();
  });

  container.append(title, metadata, detailsButton);

  return container;
}