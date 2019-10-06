import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NavController } from '@ionic/angular';

@Component({
  selector: 'app-scene-detail',
  templateUrl: './scene-detail.page.html',
  styleUrls: ['./scene-detail.page.scss'],
})
export class SceneDetailPage implements OnInit {

  constructor(private router: Router, private navCtrl: NavController) { }

  ngOnInit() {
  }

  onVisitScene() {
    // this.router.navigateByUrl('/scenery/tabs/observe');
    this.navCtrl.navigateBack('/scenery/tabs/observe');
  }
}
