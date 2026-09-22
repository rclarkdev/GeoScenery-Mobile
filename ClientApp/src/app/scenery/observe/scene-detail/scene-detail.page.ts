import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavController } from '@ionic/angular';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { VisitsService } from '../../../visits/visits.service';

@Component({
  selector: 'app-scene-detail',
  templateUrl: './scene-detail.page.html',
  styleUrls: ['./scene-detail.page.scss'],
})
export class SceneDetailPage implements OnInit {

  scene?: Scene;
  isVisiting = false;
  visitError = false;

  constructor(
    private navCtrl: NavController,
    private route: ActivatedRoute,
    private sceneryService: SceneryService,
    private visitsService: VisitsService) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/observe');
      }
      const sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/observe');
        return;
      }
      this.sceneryService.getScene(sceneId).subscribe(scene => this.scene = scene);
    });
  }

  onVisitScene() {
    if (!this.scene || this.isVisiting) {
      return;
    }

    this.isVisiting = true;
    this.visitError = false;
    this.visitsService.recordVisit(this.scene.id).subscribe({
      next: () => this.navCtrl.navigateBack('/visits'),
      error: () => {
        this.isVisiting = false;
        this.visitError = true;
      }
    });
  }
}
