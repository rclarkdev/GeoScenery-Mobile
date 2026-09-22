import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { of } from 'rxjs';

import { VisitsPage } from './visits.page';
import { VisitsService } from './visits.service';

describe('VisitsPage', () => {
  let component: VisitsPage;
  let fixture: ComponentFixture<VisitsPage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ VisitsPage ],
      providers: [{
        provide: VisitsService,
        useValue: { getUserVisits: () => of([{ id: 1, sceneId: 2, userId: 1, sceneTitle: 'Test scene', visitedAt: '2026-09-21T12:00:00Z' }]) }
      }],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(VisitsPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.visits.length).toBe(1);
  });
});
