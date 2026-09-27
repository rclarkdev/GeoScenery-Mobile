import { createScenePopupContent } from './scene-popup';
import { Scene } from '../scene.model';

describe('createScenePopupContent', () => {
  it('renders a normal title as emphasized text', () => {
    const content = createScenePopupContent(
      new Scene(1, 'Beautiful Mountain View', 'A scenic view.', 'https://example.com/view.jpg', 8),
      () => undefined
    );

    const title = content.querySelector('strong');
    expect(title).toBeTruthy();
    expect(title!.textContent).toBe('Beautiful Mountain View');
    expect(content.textContent).toContain('Rating 8 / 10');
    expect(content.querySelector('button')?.textContent).toBe('View details');
  });

  it('invokes the details action only from the explicit button', () => {
    const onViewDetails = jasmine.createSpy('onViewDetails');
    const content = createScenePopupContent(
      new Scene(1, 'Mountain View', 'A scenic view.', 'https://example.com/view.jpg', 8),
      onViewDetails
    );

    content.querySelector('button')!.click();

    expect(onViewDetails).toHaveBeenCalledTimes(1);
  });

  it('treats an img-tag XSS title as inert text instead of executable HTML', () => {
    const payload = '<img src=x onerror="alert(1)">';
    const content = createScenePopupContent(
      new Scene(2, payload, 'A scenic view.', 'https://example.com/view.jpg', 8),
      () => undefined
    );

    // The payload must not materialize as elements or attributes.
    expect(content.querySelector('img')).toBeNull();
    expect(content.querySelector('[onerror]')).toBeNull();

    // The full payload is preserved as literal text.
    expect(content.querySelector('strong')!.textContent).toBe(payload);
    expect(content.textContent).toContain(payload);
  });

  it('treats a script-tag XSS title as inert text instead of executable HTML', () => {
    const payload = '<script>alert(1)</script>';
    const content = createScenePopupContent(
      new Scene(3, payload, 'A scenic view.', 'https://example.com/view.jpg', 8),
      () => undefined
    );

    expect(content.querySelector('script')).toBeNull();
    expect(content.querySelector('strong')!.textContent).toBe(payload);
    expect(content.textContent).toContain(payload);
  });
});