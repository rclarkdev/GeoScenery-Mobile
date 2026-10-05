export type PhotoCropKind = 'scene' | 'profile';

export interface PhotoCropConfig {
  kind: PhotoCropKind;
}

export interface PhotoCropResult {
  file: File;
}
