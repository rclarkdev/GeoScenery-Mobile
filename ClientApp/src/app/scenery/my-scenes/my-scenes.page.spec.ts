import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { of } from 'rxjs';

import { MyScenesPage } from './my-scenes.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';
import { ImageUploadService } from '../../shared/image-upload.service';
import { ImageUrlPipe } from '../../shared/image-url.pipe';

describe('MyScenesPage', () => {
  let component: MyScenesPage;
  let fixture: ComponentFixture<MyScenesPage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;

  beforeEach(waitForAsync(() => {
    sceneryService = jasmine.createSpyObj('SceneryService', ['getMyScenes']);
    sceneryService.getMyScenes.and.returnValue(of([new Scene(1, 'My scene', 'Description', '/uploads/image.jpg', 8)]));
    TestBed.configureTestingModule({
      declarations: [ MyScenesPage ],
      imports: [ImageUrlPipe],
      providers: [{
        provide: SceneryService,
        useValue: sceneryService
      }, { provide: ImageUploadService, useValue: {} }],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MyScenesPage);
    component = fixture.componentInstance;
    component.ionViewWillEnter();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.myScenes.length).toBe(1);
  });

  it('reloads scenes whenever the page becomes active', () => {
    sceneryService.getMyScenes.and.returnValue(of([new Scene(2, 'New scene', 'Description', '/uploads/new.jpg', 7)]));

    component.ionViewWillEnter();

    expect(sceneryService.getMyScenes).toHaveBeenCalledTimes(2);
    expect(component.myScenes.map(scene => scene.title)).toEqual(['New scene']);
  });
});
