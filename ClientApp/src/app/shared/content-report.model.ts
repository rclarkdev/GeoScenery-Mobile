export interface CreateContentReportRequest {
  description: string;
}

export interface ContentReportReceipt {
  id: number;
  targetType: 'Profile' | 'Scene';
  targetId: number;
  createdAt: string;
}
