import { Component, OnInit } from '@angular/core';
import { Scene } from '../scene.model';
import { SceneryService } from '../scenery.service';

@Component({
  selector: 'app-my-scenes',
  templateUrl: './my-scenes.page.html',
  styleUrls: ['./my-scenes.page.scss'],
})
export class MyScenesPage implements OnInit {

  myScenes: Scene[];

  constructor(private sceneryService: SceneryService) { }

  ngOnInit() {
    // TODO: update this to handle page caching

    this.myScenes = this.sceneryService.scenery;

  }
}
