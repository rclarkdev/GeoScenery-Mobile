import { Component, OnInit } from '@angular/core';
import { Visit } from './visit.model';
import { VisitsService } from './visits.service';

@Component({
  selector: 'app-visits',
  templateUrl: './visits.page.html',
  styleUrls: ['./visits.page.scss'],
})
export class VisitsPage implements OnInit {
  visits: Visit[] = [];

  constructor(private visitsService: VisitsService) { }

  ngOnInit() {
    this.visitsService.getUserVisits().subscribe(visits => this.visits = visits);
  }

}
