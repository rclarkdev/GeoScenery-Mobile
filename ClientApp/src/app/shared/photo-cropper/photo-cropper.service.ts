import { Injectable } from '@angular/core';
import { ModalController } from '@ionic/angular';
import { ImageCropperDialogComponent } from './photo-cropper-dialog.component';
import { PhotoCropConfig, PhotoCropResult } from './photo-cropper.types';

@Injectable({ providedIn: 'root' })
export class PhotoCropperService {
  constructor(private modalController: ModalController) { }

  async crop(file: File, config: PhotoCropConfig): Promise<File | undefined> {
    const modal = await this.modalController.create({
      component: ImageCropperDialogComponent,
      componentProps: { sourceFile: file, config },
      cssClass: 'photo-crop-modal',
      showBackdrop: true,
      backdropDismiss: false,
      handle: false
    });
    await modal.present();
    const { data, role } = await modal.onDidDismiss<PhotoCropResult>();
    return role === 'confirm' ? data?.file : undefined;
  }

  async cropFromUri(uri: string, config: PhotoCropConfig): Promise<File | undefined> {
    const response = await fetch(uri);
    if (!response.ok) {
      throw new Error('The selected image could not be read.');
    }

    const blob = await response.blob();
    const mimeType = blob.type.toLowerCase();
    const extension = mimeType === 'image/png' ? 'png' : mimeType === 'image/webp' ? 'webp' : 'jpg';
    return this.crop(new File([blob], `photo.${extension}`, { type: mimeType }), config);
  }
}
