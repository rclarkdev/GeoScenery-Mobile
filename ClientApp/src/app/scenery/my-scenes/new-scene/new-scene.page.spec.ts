import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { NewScenePage } from './new-scene.page';
import { SceneryService } from '../../scenery.service';
import { ImageUploadService } from '../../../shared/image-upload.service';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';
import { PhotoCropperService } from '../../../shared/photo-cropper/photo-cropper.service';

describe('NewScenePage', () => {
  let component: NewScenePage;
  let fixture: ComponentFixture<NewScenePage>;
  let photoCropperService: jasmine.SpyObj<PhotoCropperService>;

  beforeEach(waitForAsync(() => {
    photoCropperService = jasmine.createSpyObj('PhotoCropperService', ['crop', 'cropFromUri']);
    photoCropperService.crop.and.resolveTo(undefined);
    photoCropperService.cropFromUri.and.resolveTo(undefined);
    TestBed.configureTestingModule({
      declarations: [ NewScenePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot(), ImageUrlPipe],
      providers: [
        { provide: SceneryService, useValue: { createScene: jasmine.createSpy('createScene').and.returnValue(of({})) } },
        {
          provide: ImageUploadService,
          useValue: { uploadSelectedImage: jasmine.createSpy('uploadSelectedImage').and.returnValue(of({ url: '/uploads/image.jpg' })) }
        },
        { provide: PhotoCropperService, useValue: photoCropperService },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(NewScenePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not submit an invalid scene form', () => {
    component.onSave();

    expect(TestBed.inject(SceneryService).createScene).not.toHaveBeenCalled();
  });

  it('should create a scene and navigate back after a valid submission', async () => {
    const service = TestBed.inject(SceneryService);
    const imageUploadService = TestBed.inject(ImageUploadService);
    const navController = TestBed.inject(NavController);
    component.sceneForm.setValue({
      title: 'New scene',
      description: 'A description',
      imageUrl: 'blob:http://localhost/photo-id',
      rating: 8,
      isPublic: true,
      tags: 'sunset, beach',
      latitude: null,
      longitude: null
    });

    await component.onSave();

  expect(imageUploadService.uploadSelectedImage).toHaveBeenCalledWith('blob:http://localhost/photo-id', 'scene');
    expect(service.createScene).toHaveBeenCalledWith({
      title: 'New scene',
      description: 'A description',
      imageUrl: '/uploads/image.jpg',
      rating: 8,
      isPublic: true,
      latitude: null,
      longitude: null,
      tags: ['sunset', 'beach']
    });
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/my-scenes');
  });

  it('sends a private visibility choice when creating a scene', async () => {
    const service = TestBed.inject(SceneryService);
    component.sceneForm.patchValue({
      title: 'Private scene',
      description: 'Only for me',
      imageUrl: 'blob:http://localhost/private-photo',
      rating: 6,
      isPublic: false
    });

    await component.onSave();

    expect(service.createScene).toHaveBeenCalledWith(jasmine.objectContaining({ isPublic: false }));
  });

  it('keeps a confirmed crop pending and uploads its File only when the scene is saved', async () => {
    const croppedFile = new File(['crop'], 'cropped.jpg', { type: 'image/jpeg' });
    photoCropperService.crop.and.resolveTo(croppedFile);
    const fileInput = { files: [new File(['source'], 'source.jpg', { type: 'image/jpeg' })], value: 'selected' } as any;
    await component.onWebFileSelected({ target: fileInput } as any);
    component.sceneForm.patchValue({ title: 'A scene', description: 'A view' });

    await component.onSave();

    expect(photoCropperService.crop).toHaveBeenCalledWith(jasmine.any(File), { kind: 'scene' });
    expect(TestBed.inject(ImageUploadService).uploadSelectedImage).toHaveBeenCalledWith(croppedFile, 'scene');
    expect(fileInput.value).toBe('');
  });

  it('does not replace a scene image when the crop is canceled', async () => {
    const previousImage = 'https://example.com/previous.jpg';
    component.sceneForm.patchValue({ imageUrl: previousImage });
    photoCropperService.crop.and.resolveTo(undefined);

    await component.onWebFileSelected({
      target: { files: [new File(['source'], 'source.jpg', { type: 'image/jpeg' })], value: 'selected' }
    } as any);

    expect(component.sceneForm.controls.imageUrl.value).toBe(previousImage);
  });
});
