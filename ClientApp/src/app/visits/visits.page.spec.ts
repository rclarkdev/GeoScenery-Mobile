import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';

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
    expect(component.isLoading).toBe(false);
    expect(component.loadError).toBe(false);
  });

  it('does not announce an empty state while the visits request is pending', () => {
    const pending = new Subject<any>();
    TestBed.inject(VisitsService).getUserVisits = () => pending;
    component.ngOnInit();
    fixture.detectChanges();

    expect(component.isLoading).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Loading visits');
    expect(fixture.nativeElement.textContent).not.toContain('No visits recorded');
  });

  it('shows an error and retries after a failed visits request', () => {
    const visitsService = TestBed.inject(VisitsService);
    spyOn(visitsService, 'getUserVisits').and.returnValues(
      throwError(() => new Error('offline')),
      of([{ id: 2, sceneId: 3, userId: 1, sceneTitle: 'Recovered scene', visitedAt: '2026-10-09T12:00:00Z' }])
    );

    component.retryLoad();
    fixture.detectChanges();

    expect(component.loadError).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Unable to load your visits');
    expect(fixture.nativeElement.textContent).toContain('Try again');

    component.retryLoad();
    fixture.detectChanges();

    expect(component.loadError).toBe(false);
    expect(component.visits[0].sceneTitle).toBe('Recovered scene');
  });
});
