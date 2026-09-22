import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { NavController } from '@ionic/angular';
import { SceneryService } from '../../scenery.service';

@Component({
  selector: 'app-new-scene',
  templateUrl: './new-scene.page.html',
  styleUrls: ['./new-scene.page.scss'],
})
export class NewScenePage {
  isSaving = false;
  saveError = false;

  readonly sceneForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    imageUrl: ['', [Validators.required, Validators.maxLength(2048), Validators.pattern(/^https?:\/\/.+/i)]],
    rating: [0, [Validators.min(0), Validators.max(10)]]
  });

  constructor(
    private formBuilder: FormBuilder,
    private sceneryService: SceneryService,
    private navCtrl: NavController
  ) { }

  onSave() {
    if (this.sceneForm.invalid || this.isSaving) {
      this.sceneForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    this.sceneryService.createScene(this.sceneForm.getRawValue()).subscribe({
      next: () => this.navCtrl.navigateBack('/scenery/tabs/my-scenes'),
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }
}
