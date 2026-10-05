import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormBuilder, Validators } from '@angular/forms';
import { Camera, CameraDirection, CameraResultType, CameraSource } from '@capacitor/camera';
import { Capacitor } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../../scenery.service';
import { NavController } from '@ionic/angular';
import { Scene } from '../../scene.model';
import { firstValueFrom } from 'rxjs';
import { ImageUploadService } from '../../../shared/image-upload.service';
import { PhotoCropperService } from '../../../shared/photo-cropper/photo-cropper.service';

@Component({
  selector: 'app-edit-scene',
  templateUrl: './edit-scene.page.html',
  styleUrls: ['./edit-scene.page.scss'],
})
export class EditScenePage implements OnInit, OnDestroy {

  scene?: Scene;
  isSaving = false;
  saveError = false;
  isLocating = false;
  locationError = false;
  photoError = false;
  private pendingImageFile: File | null = null;
  private previewObjectUrl: string | null = null;
  readonly sceneForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    imageUrl: ['', [Validators.required]],
    rating: [0, [Validators.min(0), Validators.max(10)]],
    isPublic: [true],
    tags: [''],
    latitude: this.formBuilder.control<number | null>(null, [Validators.min(-90), Validators.max(90)]),
    longitude: this.formBuilder.control<number | null>(null, [Validators.min(-180), Validators.max(180)])
  });

  constructor(
    private route: ActivatedRoute,
    private sceneryService: SceneryService,
    private imageUploadService: ImageUploadService,
    private navCtrl: NavController,
    private formBuilder: FormBuilder,
    private photoCropperService: PhotoCropperService
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      const sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      this.sceneryService.getScene(sceneId).subscribe(scene => {
        this.scene = scene;
        this.sceneForm.setValue({
          title: scene.title,
          description: scene.description,
          imageUrl: scene.imageUrl,
          rating: scene.rating,
          isPublic: scene.isPublic,
          tags: scene.tags.join(', '),
          latitude: scene.latitude ?? null,
          longitude: scene.longitude ?? null
        });
      });

    });
  }

  ngOnDestroy(): void {
    this.revokePreviewObjectUrl();
  }

  async useCurrentLocation() {
    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await Geolocation.getCurrentPosition();
      this.sceneForm.patchValue({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude
      });
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  async onPickImage(fileInput: HTMLInputElement) {
    this.photoError = false;
    if (!Capacitor.isNativePlatform()) {
      fileInput.click();
      return;
    }

    try {
      // Prompt lets the user choose between the camera and their photo library
      const photo = await Camera.getPhoto({
        quality: 80,
        direction: CameraDirection.Front,
        resultType: CameraResultType.Uri,
        source: CameraSource.Prompt
      });
      if (photo.webPath) {
        const file = await this.photoCropperService.cropFromUri(photo.webPath, { kind: 'scene' });
        if (file) {
          this.setPendingImage(file);
        }
      }
    } catch (error) {
      if (!(error instanceof Error) || !/cancel/i.test(error.message)) {
        this.photoError = true;
      }
    }
  }

  async onWebFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.photoError = false;
    try {
      const cropped = await this.photoCropperService.crop(file, { kind: 'scene' });
      if (cropped) {
        this.setPendingImage(cropped);
      }
    } catch {
      this.photoError = true;
    }
  }

  async onSave() {
    if (!this.scene || this.sceneForm.invalid || this.isSaving) {
      this.sceneForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    const { tags, ...rest } = this.sceneForm.getRawValue();
    try {
      const imageUrl = (await firstValueFrom(
        this.imageUploadService.uploadSelectedImage(this.pendingImageFile ?? rest.imageUrl, 'scene')
      )).url;
      await firstValueFrom(this.sceneryService.updateScene(this.scene.id, {
        ...rest,
        imageUrl,
        tags: tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0)
      }));
      await this.navCtrl.navigateBack(`/scenery/tabs/my-scenes/${this.scene?.id}`);
    } catch {
      this.isSaving = false;
      this.saveError = true;
    }
  }

  private setPendingImage(file: File): void {
    this.revokePreviewObjectUrl();
    this.pendingImageFile = file;
    this.previewObjectUrl = URL.createObjectURL(file);
    this.sceneForm.patchValue({ imageUrl: this.previewObjectUrl });
    this.sceneForm.controls.imageUrl.markAsTouched();
  }

  private revokePreviewObjectUrl(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
  }
}
