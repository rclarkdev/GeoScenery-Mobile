export class User {
    constructor(
        public id: number,
        public displayName: string,
        public email: string | null,
        public profileImageUrl?: string,
        public latitude?: number,
        public longitude?: number,
        public followerCount = 0,
        public followingCount = 0,
        public isFollowedByCurrentUser = false,
        public createdAt?: string
    ) {

    }
}
