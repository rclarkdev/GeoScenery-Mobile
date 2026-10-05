import { ModalController } from '@ionic/angular';
import { ImageCropperDialogComponent } from './photo-cropper-dialog.component';

describe('ImageCropperDialogComponent', () => {
  let component: ImageCropperDialogComponent;
  let modalController: jasmine.SpyObj<ModalController>;

  beforeEach(() => {
    modalController = jasmine.createSpyObj<ModalController>('ModalController', ['dismiss']);
    component = new ImageCropperDialogComponent(modalController);
    component.sourceFile = new File(['photo'], 'photo.png', { type: 'image/png' });
    component.config = { kind: 'scene' };
    component.ngOnInit();
  });

  it('loads a valid PNG and configures a scene crop starting at 16:9', () => {
    expect(component.sourceIsValid).toBeTrue();
    expect(component.aspectRatio).toBe(16 / 9);
    expect(component.outputFormat).toBe('png');
    expect(component.resizeSide).toBe(1600);
  });

  it('provides the required scene presets and a free crop', () => {
    expect(component.presets.map(preset => preset.ratio)).toEqual([16 / 9, 4 / 3, 1]);
    component.setPreset(component.presets[1]);
    expect(component.aspectRatio).toBe(4 / 3);
    expect(component.maintainAspectRatio).toBeTrue();
    component.setFreeCrop();
    expect(component.maintainAspectRatio).toBeFalse();
  });

  it('configures profile crops as round-previewed 512 square output', () => {
    component.config = { kind: 'profile' };
    component.ngOnInit();

    expect(component.isProfile).toBeTrue();
    expect(component.aspectRatio).toBe(1);
    expect(component.resizeSide).toBe(512);
  });

  it('confirms only the generated Blob as a File', async () => {
    component.onCropperReady();
    component.cropper = { crop: jasmine.createSpy('crop').and.resolveTo({ blob: new Blob(['crop'], { type: 'image/png' }) }) } as any;

    await component.confirm();

    const [data, role] = modalController.dismiss.calls.mostRecent().args as any[];
    expect(role).toBe('confirm');
    expect(data.file).toEqual(jasmine.any(File));
    expect(data.file.type).toBe('image/png');
  });

  it('leaves the dialog open when canceled and does not emit a crop', () => {
    component.cancel();

    expect(modalController.dismiss).toHaveBeenCalledWith(undefined, 'cancel');
  });

  it('resets proportion, zoom, and rotation', () => {
    component.setPreset(component.presets[1]);
    component.zoomIn();
    component.rotate();
    expect(component.canvasRotation).toBe(1);

    component.reset();

    expect(component.aspectRatio).toBe(16 / 9);
    expect(component.transform.scale).toBe(1);
    expect(component.canvasRotation).toBe(0);
  });

  it('reports cropper decode and generation errors safely', async () => {
    component.onLoadFailed();
    expect(component.errorMessage).toContain('could not be opened');

    component.onCropperReady();
    component.cropper = { crop: jasmine.createSpy('crop').and.rejectWith(new Error('decode details')) } as any;
    await component.confirm();

    expect(component.errorMessage).toContain('could not be created');
    expect(modalController.dismiss).not.toHaveBeenCalled();
  });

  it('rejects an oversized source before invoking the cropper', async () => {
    component.sourceFile = new File([new Uint8Array(25 * 1024 * 1024 + 1)], 'large.jpg', { type: 'image/jpeg' });

    expect(component.sourceIsValid).toBeFalse();
    expect(component.isReady).toBeFalse();
    expect(component.cropper).toBeUndefined();
  });
});
