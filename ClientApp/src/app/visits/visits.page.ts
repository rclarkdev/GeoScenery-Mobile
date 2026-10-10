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
  isLoading = true;
  loadError = false;

  constructor(private visitsService: VisitsService) { }

  ngOnInit() {
    this.loadVisits();
  }

  retryLoad(): void {
    this.loadVisits();
  }

  private loadVisits(): void {
    this.isLoading = true;
    this.loadError = false;
    this.visitsService.getUserVisits().subscribe({
      next: visits => {
        this.visits = visits;
        this.isLoading = false;
      },
      error: () => {
        this.visits = [];
        this.isLoading = false;
        this.loadError = true;
      }
    });
  }
}
