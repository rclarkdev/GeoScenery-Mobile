import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { SceneRequest, SceneryService } from './scenery.service';
import { Scene } from './scene.model';

describe('SceneryService', () => {
  let service: SceneryService;
  let http: HttpTestingController;
  const scenesUrl = `${environment.apiUrl}/api/scenes`;
  const scene: Scene = new Scene(7, 'Canyon overlook', 'A scenic overlook.', 'https://example.com/scene.jpg', 8);
  const request: SceneRequest = {
    title: scene.title,
    description: scene.description,
    imageUrl: scene.imageUrl,
    rating: scene.rating
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(SceneryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets all scenes', () => {
    service.getScenery().subscribe(result => expect(result).toEqual([scene]));
    const request = http.expectOne(scenesUrl);
    expect(request.request.method).toBe('GET');
    request.flush([scene]);
  });

  it('gets one scene by id', () => {
    service.getScene(scene.id).subscribe(result => expect(result).toEqual(scene));
    const request = http.expectOne(`${scenesUrl}/${scene.id}`);
    expect(request.request.method).toBe('GET');
    request.flush(scene);
  });

  it('creates a scene', () => {
    service.createScene(request).subscribe(result => expect(result).toEqual(scene));
    const httpRequest = http.expectOne(scenesUrl);
    expect(httpRequest.request.method).toBe('POST');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush(scene);
  });

  it('updates a scene', () => {
    service.updateScene(scene.id, request).subscribe(result => expect(result).toEqual(scene));
    const httpRequest = http.expectOne(`${scenesUrl}/${scene.id}`);
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush(scene);
  });

  it('deletes a scene', () => {
    service.deleteScene(scene.id).subscribe();
    const httpRequest = http.expectOne(`${scenesUrl}/${scene.id}`);
    expect(httpRequest.request.method).toBe('DELETE');
    httpRequest.flush(null);
  });
});
