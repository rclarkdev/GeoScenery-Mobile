import { Component, OnInit } from '@angular/core';
import { Scene } from '../../scene.model';
import { ActivatedRoute } from '@angular/router';
import { NavController } from '@ionic/angular';
import { SceneryService } from '../../scenery.service';

@Component({
  selector: 'app-share-scenery',
  templateUrl: './share-scenery.page.html',
  styleUrls: ['./share-scenery.page.scss'],
})
export class ShareSceneryPage implements OnInit {

  scene: Scene;

  constructor(private route: ActivatedRoute, private navCtrl: NavController, private sceneryService: SceneryService) { }

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
