import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormBuilder, Validators } from '@angular/forms';
import { SceneryService } from '../../scenery.service';
import { NavController } from '@ionic/angular';
import { Scene } from '../../scene.model';

@Component({
  selector: 'app-edit-scene',
  templateUrl: './edit-scene.page.html',
  styleUrls: ['./edit-scene.page.scss'],
})
export class EditScenePage implements OnInit {

  scene?: Scene;
  isSaving = false;
  saveError = false;
  readonly sceneForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    imageUrl: ['', [Validators.required, Validators.maxLength(2048), Validators.pattern(/^https?:\/\/.+/i)]],
    rating: [0, [Validators.min(0), Validators.max(10)]]
  });

  constructor(
    private route: ActivatedRoute,
    private sceneryService: SceneryService,
    private navCtrl: NavController,
    private formBuilder: FormBuilder
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
          rating: scene.rating
        });
      });

    });
  }

  onSave() {
    if (!this.scene || this.sceneForm.invalid || this.isSaving) {
      this.sceneForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    this.sceneryService.updateScene(this.scene.id, this.sceneForm.getRawValue()).subscribe({
      next: () => this.navCtrl.navigateBack(`/scenery/tabs/my-scenes/${this.scene?.id}`),
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }

}
