import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { IonicModule, ModalController } from '@ionic/angular';
import { ImageCropperComponent, ImageCroppedEvent, ImageTransform } from 'ngx-image-cropper';
import { PhotoCropConfig } from './photo-cropper.types';

const MAX_SOURCE_BYTES = 25 * 1024 * 1024;
const MAX_OUTPUT_BYTES = 5 * 1024 * 1024;
const MAX_SCENE_SIDE = 1600;
const MAX_PROFILE_SIDE = 512;

@Component({
  selector: 'app-photo-cropper-dialog',
  standalone: true,
  imports: [CommonModule, IonicModule, ImageCropperComponent],
  templateUrl: './photo-cropper-dialog.component.html',
  styleUrls: ['./photo-cropper-dialog.component.scss']
})
export class ImageCropperDialogComponent implements OnInit {
  @Input() sourceFile!: File;
  @Input() config!: PhotoCropConfig;
  @ViewChild(ImageCropperComponent) cropper?: ImageCropperComponent;

  readonly presets = [
    { label: '16:9', ratio: 16 / 9 },
    { label: '4:3', ratio: 4 / 3 },
    { label: 'Square', ratio: 1 }
  ];
  aspectRatio = 16 / 9;
  maintainAspectRatio = true;
  canvasRotation = 0;
  transform: ImageTransform = { scale: 1 };
  isReady = false;
  isCropping = false;
  errorMessage: string | null = null;
  readonly quality = 85;
  outputFormat: 'png' | 'jpeg' = 'jpeg';
  resizeSide = MAX_SCENE_SIDE;

  constructor(private modalController: ModalController) { }

  ngOnInit(): void {
    this.outputFormat = this.sourceFile?.type?.toLowerCase() === 'image/png' ? 'png' : 'jpeg';
    this.resizeSide = this.config?.kind === 'profile' ? MAX_PROFILE_SIDE : MAX_SCENE_SIDE;
    if (this.config?.kind === 'profile') {
      this.aspectRatio = 1;
    }
  }

  get isProfile(): boolean {
    return this.config?.kind === 'profile';
  }

  get sourceIsValid(): boolean {
    return !!this.sourceFile
      && ['image/jpeg', 'image/png', 'image/webp'].includes(this.sourceFile.type.toLowerCase())
      && this.sourceFile.size > 0
      && this.sourceFile.size <= MAX_SOURCE_BYTES;
  }

  onCropperReady(): void {
    this.isReady = true;
    this.errorMessage = null;
  }

  onLoadFailed(): void {
    this.isReady = false;
    this.errorMessage = 'This photo could not be opened. Try a JPEG, PNG, or WebP image.';
  }

  setPreset(preset: { ratio: number }): void {
    this.aspectRatio = preset.ratio;
    this.maintainAspectRatio = true;
  }

  setFreeCrop(): void {
    this.maintainAspectRatio = false;
  }

  zoomIn(): void {
    this.transform = { ...this.transform, scale: Math.min((this.transform.scale ?? 1) + 0.1, 3) };
  }

  zoomOut(): void {
    this.transform = { ...this.transform, scale: Math.max((this.transform.scale ?? 1) - 0.1, 1) };
  }

  rotate(): void {
    this.canvasRotation = (this.canvasRotation + 1) % 4;
  }

  reset(): void {
    this.aspectRatio = this.isProfile ? 1 : 16 / 9;
    this.maintainAspectRatio = true;
    this.canvasRotation = 0;
    this.transform = { scale: 1 };
    this.cropper?.resetCropperPosition();
    this.errorMessage = null;
  }

  async confirm(): Promise<void> {
    if (!this.sourceIsValid || !this.isReady || !this.cropper || this.isCropping) {
      return;
    }

    this.isCropping = true;
    this.errorMessage = null;
    try {
      const event: ImageCroppedEvent | null = await this.cropper.crop('blob');
      const blob = event?.blob;
      if (!blob) {
        throw new Error('The crop could not be generated.');
      }
      if (blob.size > MAX_OUTPUT_BYTES) {
        throw new Error('This crop is too large to upload. Choose a smaller area or a different photo.');
      }
      const extension = this.outputFormat === 'png' ? 'png' : 'jpg';
      const file = new File([blob], `cropped-photo.${extension}`, {
        type: this.outputFormat === 'png' ? 'image/png' : 'image/jpeg',
        lastModified: Date.now()
      });
      await this.modalController.dismiss({ file }, 'confirm');
    } catch (error) {
      this.errorMessage = error instanceof Error && error.message.includes('too large')
        ? error.message
        : 'The crop could not be created. Please adjust the photo and try again.';
      this.isCropping = false;
    }
  }

  cancel(): void {
    void this.modalController.dismiss(undefined, 'cancel');
  }
}
