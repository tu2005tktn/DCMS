import { Button, Empty, Modal } from 'antd';
import type { FamilyMember } from './types';

type Props = {
  member: Pick<FamilyMember, 'id' | 'fullName'> | null;
  onClose: () => void;
};

// Placeholder only: record access will require the linked-record API and authorization.
export default function FamilyMemberRecordsModal({ member, onClose }: Props) {
  return <Modal
    open={member !== null}
    title={member ? `Hồ sơ của ${member.fullName}` : 'Hồ sơ người thân'}
    onCancel={onClose}
    keyboard
    focusTriggerAfterClose
    className="customer-family-records-modal"
    footer={<Button type="primary" onClick={onClose}>Đóng</Button>}
  >
    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có hồ sơ nào" />
  </Modal>;
}
