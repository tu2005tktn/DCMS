import { Button, Modal } from 'antd';
import type { FamilyMember } from './types';
import FamilyRecordContent from './FamilyRecordContent';

type Props = {
  member: Pick<FamilyMember, 'id' | 'fullName'> | null;
  onClose: () => void;
};

export default function FamilyMemberRecordsModal({ member, onClose }: Props) {
  return <Modal
    open={member !== null}
    title={member ? `Hồ sơ của ${member.fullName}` : 'Hồ sơ người thân'}
    onCancel={onClose}
    keyboard
    focusTriggerAfterClose
    className="customer-family-records-modal"
    width={900}
    destroyOnClose
    footer={<Button type="primary" onClick={onClose}>Đóng</Button>}
  >
    {member && <FamilyRecordContent key={member.id} member={member} />}
  </Modal>;
}
