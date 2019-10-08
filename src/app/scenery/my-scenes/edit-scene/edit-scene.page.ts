import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SceneryService } from '../../scenery.service';
import { NavController } from '@ionic/angular';
import { Scene } from '../../scene.model';

@Component({
  selector: 'app-edit-scene',
  templateUrl: './edit-scene.page.html',
  styleUrls: ['./edit-scene.page.scss'],
})
export class EditScenePage implements OnInit {

  scene: Scene;

  constructor(
    private route: ActivatedRoute,
    private sceneryService: SceneryService,
    private navCtrl: NavController
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      this.scene = this.sceneryService.getScene(paramMap.get('sceneId'));

    });
  }

}
