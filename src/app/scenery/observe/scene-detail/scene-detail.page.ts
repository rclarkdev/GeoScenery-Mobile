import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavController } from '@ionic/angular';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';

@Component({
  selector: 'app-scene-detail',
  templateUrl: './scene-detail.page.html',
  styleUrls: ['./scene-detail.page.scss'],
})
export class SceneDetailPage implements OnInit {

  scene: Scene;

  constructor(
    private navCtrl: NavController,
    private route: ActivatedRoute,
    private sceneryService: SceneryService) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/observe');
      }
      this.scene = this.sceneryService.getScene(paramMap.get('sceneId'));
    });
  }

  onVisitScene() {
    // this.router.navigateByUrl('/scenery/tabs/observe');
    this.navCtrl.navigateBack('/scenery/tabs/observe');
  }
}
