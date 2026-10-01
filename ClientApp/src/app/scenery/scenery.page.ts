import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-scenery',
  templateUrl: './scenery.page.html',
  styleUrls: ['./scenery.page.scss'],
})
export class SceneryPage implements OnInit {

  constructor(private authService: AuthService) { }

  get showAdminTab(): boolean {
    return this.authService.isAdmin;
  }

  ngOnInit() {
  }

}
