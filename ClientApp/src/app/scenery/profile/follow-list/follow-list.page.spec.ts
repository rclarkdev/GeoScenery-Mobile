import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { FollowListPage } from './follow-list.page';
import { UserService } from '../../../auth/user.service';
import { UserSummary } from '../../../auth/user-summary.model';

describe('FollowListPage', () => {
  let component: FollowListPage;
  let fixture: ComponentFixture<FollowListPage>;

  function configure(mode: string | undefined, users: UserSummary[]) {
    TestBed.configureTestingModule({
      declarations: [ FollowListPage ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(new Map([['userId', '1']])),
            snapshot: { data: { mode } }
          }
        },
        {
          provide: UserService,
          useValue: {
            getFollowers: jasmine.createSpy('getFollowers').and.returnValue(of(users)),
            getFollowing: jasmine.createSpy('getFollowing').and.returnValue(of(users))
          }
        }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    });
  }

  it('loads followers by default', waitForAsync(() => {
    const users = [new UserSummary(2, 'Bob')];
    configure('followers', users);
    fixture = TestBed.createComponent(FollowListPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.title).toBe('Followers');
    expect(component.users).toEqual(users);
    expect(TestBed.inject(UserService).getFollowers).toHaveBeenCalledWith(1);
  }));

  it('loads following when mode is following', waitForAsync(() => {
    const users = [new UserSummary(3, 'Carol')];
    configure('following', users);
    fixture = TestBed.createComponent(FollowListPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.title).toBe('Following');
    expect(component.users).toEqual(users);
    expect(TestBed.inject(UserService).getFollowing).toHaveBeenCalledWith(1);
  }));
});
