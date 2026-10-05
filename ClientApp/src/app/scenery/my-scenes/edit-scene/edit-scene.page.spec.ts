import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { EditScenePage } from './edit-scene.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { ImageUploadService } from '../../../shared/image-upload.service';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';
import { PhotoCropperService } from '../../../shared/photo-cropper/photo-cropper.service';

describe('EditScenePage', () => {
  let component: EditScenePage;
  let fixture: ComponentFixture<EditScenePage>;
  let photoCropperService: jasmine.SpyObj<PhotoCropperService>;

  beforeEach(waitForAsync(() => {
    photoCropperService = jasmine.createSpyObj('PhotoCropperService', ['crop', 'cropFromUri']);
    photoCropperService.crop.and.resolveTo(undefined);
    photoCropperService.cropFromUri.and.resolveTo(undefined);
    TestBed.configureTestingModule({
      declarations: [ EditScenePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot(), ImageUrlPipe],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: {
          getScene: jasmine.createSpy('getScene').and.returnValue(of(new Scene(1, 'Test scene', 'Description', '/uploads/image.jpg', 8, undefined, undefined, [], undefined, undefined, 0, undefined, 1, undefined, undefined, false))),
          updateScene: jasmine.createSpy('updateScene').and.returnValue(of(new Scene(1, 'Updated scene', 'Updated description', 'https://example.com/updated.jpg', 9)))
        } },
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
    fixture = TestBed.createComponent(EditScenePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.scene?.id).toBe(1);
    expect(component.sceneForm.controls.isPublic.value).toBe(false);
  });

  it('should update the scene and navigate back after a valid submission', async () => {
    const service = TestBed.inject(SceneryService);
    const navController = TestBed.inject(NavController);

    component.sceneForm.patchValue({ title: 'Updated scene', rating: 9 });
    await component.onSave();

    expect(service.updateScene).toHaveBeenCalledWith(1, {
      title: 'Updated scene',
      description: 'Description',
      imageUrl: '/uploads/image.jpg',
      rating: 9,
      isPublic: false,
      latitude: null,
      longitude: null,
      tags: []
    });
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/my-scenes/1');
  });

  it('sends a changed public/private setting when editing a scene', async () => {
    const service = TestBed.inject(SceneryService);
    component.sceneForm.patchValue({ isPublic: true });

    await component.onSave();

    expect(service.updateScene).toHaveBeenCalledWith(1, jasmine.objectContaining({ isPublic: true }));
  });

  it('uploads the confirmed crop File on save without replacing the existing image before confirmation', async () => {
    const currentImage = component.sceneForm.controls.imageUrl.value;
    const croppedFile = new File(['crop'], 'cropped.jpg', { type: 'image/jpeg' });
    photoCropperService.crop.and.resolveTo(croppedFile);
    await component.onWebFileSelected({
      target: { files: [new File(['source'], 'source.jpg', { type: 'image/jpeg' })], value: 'selected' }
    } as any);

    expect(component.sceneForm.controls.imageUrl.value).not.toBe(currentImage);
    await component.onSave();

    expect(TestBed.inject(ImageUploadService).uploadSelectedImage).toHaveBeenCalledWith(croppedFile, 'scene');
  });

  it('preserves the existing scene image when crop is canceled', async () => {
    const currentImage = component.sceneForm.controls.imageUrl.value;
    photoCropperService.crop.and.resolveTo(undefined);

    await component.onWebFileSelected({
      target: { files: [new File(['source'], 'source.jpg', { type: 'image/jpeg' })], value: 'selected' }
    } as any);

    expect(component.sceneForm.controls.imageUrl.value).toBe(currentImage);
  });
});
